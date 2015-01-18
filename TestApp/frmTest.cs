﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using CSharpQuery.IndexCreation;
using System.Globalization;
using CSharpQuery.Index;
using System.Diagnostics;
using CSharpQuery.QueryEngine;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Threading;
using System.Configuration;
using System.IO;
using System.Data.SqlServerCe;
using CSharpQuery.Thesaurus;
using CSharpQuery.WordBreaker;

namespace TestApp {
	public partial class frmTest : Form {

		private const string IndexDir = @"..\..\Index\";

		FreeTextQuery<int> Query;
		TextIndexReader<int> IndexReader;
		List<QueryResult<int>> previousQuery;

		public frmTest() {
			InitializeComponent();
		}

		public string ConnStr {
			get { return ConfigurationManager.ConnectionStrings["ConnStr"].ConnectionString; }
		}

		public string DefaultPath {
			get { return ConfigurationManager.AppSettings["DefaultPath"]; }
		}

		protected string IndexLocation { get { return @"..\..\Index\"; } }
		
		private void btnRetreiveFromIndex_Click(object sender, EventArgs e) {

			// QUERY TEST
			Stopwatch sw = Stopwatch.StartNew();

			//var freeTextQuery = new FreeTextQuery("Bible", path, new CultureInfo("en-US"));
			//var freeTextQuery = new FreeTextQuery(new TextFileAccessContext("Bible", path, new CultureInfo("en-US")));
			if (Query == null)
				Query = LoadIndex();

			List<QueryResult<int>> result = Query.SearchFreeTextQuery(IndexReader.GetTextIndex(), txtCriteria.Text);
			sw.Stop();

			previousQuery = result;
			lblStopwatch.Text = string.Format("Query took: {0} MS / # Of Rows: {1}", sw.Elapsed.TotalMilliseconds, result.Count);

			lbResults.Items.Clear();
			foreach (QueryResult<int> r in result) {

				lbResults.Items.Add(string.Format("{0};\t Key: {1};\t Rank: {2};\t MO: {3};\t LPI: {4};\t STP: {5};\t WM: {6}",
					string.Join(", ", r.WordIndexes.Select(v => v.Word).ToArray()), r.Key, r.Rank, r.multipleOccurance, r.lowPhraseIndex, r.searchTermsProximity, r.wordMatching));
			}
		}

		private FreeTextQuery<int> LoadIndex()
		{
			var context = new TextFileAccessContext("Bible", IndexLocation, new CultureInfo("en-US"));
			IndexReader = new TextIndexReader<int>(context);
			return new FreeTextQuery<int>(
				new DefaultWordBreaker(
					new WordBreakingInformationRetriever(context.Directory, context.Culture)), 
					new DefaultThesaurus(new ThesaurusDictionaryRetriever(context.Directory)), 
					new WordRefEqualityComparer<int>(), new TextIndexSearcher<int>());
		}

		private void button1_Click_1(object sender, EventArgs e)
		{
			IndexInfo index;
			index = ExtractIndex();
			
			// Populate the Database
			using (SqlCeConnection connection = new SqlCeConnection(ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString))
			{
				connection.Open();
				string sql = string.Empty;
				foreach (Book b in index.Books)
				{
					sql = string.Format("INSERT INTO Book (BookNum, BookName) VALUES ({0}, '{1}')", b.BookNumber, b.Name.Replace("'", "''"));
					SqlCeCommand cmd = new SqlCeCommand(sql, connection);
					cmd.ExecuteNonQuery();
					sql = "SELECT MAX(BookID) FROM Book";
					cmd.CommandText = sql;
					int bookId = (int)cmd.ExecuteScalar();

					foreach (Verse v in b.Verses)
					{
						sql = string.Format("INSERT INTO Verse (BookID, ChapterNum, VerseNum, VerseText) VALUES ({0}, {1}, {2}, '{3}')",
							bookId, v.Chapter, v.VerseNum, v.Text.Replace("'", "''"));
						cmd.CommandText = sql;
						cmd.ExecuteNonQuery();
					}
				}
			}
		}

		private static IndexInfo ExtractIndex()
		{
			IndexInfo index = null;
			// Load the file
			OpenFileDialog dlg = new OpenFileDialog();
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				using (StreamReader sr = new StreamReader(File.OpenRead(dlg.FileName)))
				{
					string line = string.Empty;
					IndexBuilder info = new IndexBuilder();
					while ((line = sr.ReadLine()) != null)
					{
						info.ProcessLine(line);
					}
					index = info.Index;
				}
			}
			return index;
		}

		private void btnCreateIndex_Click(object sender, EventArgs e)
		{
			string sql = "SELECT VerseID, VerseText FROM Verse";
			using (SqlCeConnection conn = new SqlCeConnection(ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString))
			{
				conn.Open();
				var rdr = new SqlCeCommand(sql, conn).ExecuteReader();

				var context = new TextFileAccessContext("Bible", IndexDir, new CultureInfo("en-US"));
				var textIndexSaver = new TextIndexSaver<int>(context);

				var wordBreaker = new DefaultWordBreaker(new WordBreakingInformationRetriever(context.Directory, context.Culture)) { DatabasePath = context.Directory};

				var textIndexFiller = new TextIndexFiller<int>(wordBreaker);
				var indexCreator = new IndexCreator<int>(textIndexFiller);

				var index = indexCreator.CreateIndex(new BibleVersuses(rdr));
			   
				textIndexSaver.SaveIndex(index);

				rdr.Close();
			}
		}

		public class BibleVersuses : IEnumerable<Phrase<int>>
		{
			private readonly IDataReader dataReader;

			public BibleVersuses(IDataReader dataReader)
			{
				this.dataReader = dataReader;
			}

			public IEnumerator<Phrase<int>> GetEnumerator()
			{
				while(dataReader.Read())
				{
					yield return new Phrase<int>
									 {
										 Key = (int)dataReader["VerseID"],
										 Text = (string)dataReader["VerseText"]
									 };
				}
				yield break;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		private void lbResults_DoubleClick(object sender, EventArgs e)
		{
			// Show the bible verse
			if (lbResults.SelectedIndex >= 0)
			{
				string value = lbResults.SelectedItem.ToString();
				int keyIndex = value.IndexOf("Key: ") + 4;
				string key = value.Substring(keyIndex, value.IndexOf('\t', keyIndex) - keyIndex).Trim(';').Trim();
				int verseId = int.Parse(key);

				// Get the verse
				string sql = "SELECT v.*, b.* FROM Verse v INNER JOIN Book b ON v.BookID = b.BookID WHERE VerseID = " + verseId;
				string verse = string.Empty;
				using (SqlCeConnection conn = new SqlCeConnection(ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString))
				{
					conn.Open();
					SqlCeDataReader rdr = new SqlCeCommand(sql, conn).ExecuteReader();
					rdr.Read();
					verse = string.Format("{0} {1}:{2} - {3}", rdr["BookName"], rdr["BookNum"], rdr["VerseNum"], rdr["VerseText"]);
					rdr.Close();
				}
				MessageBox.Show(verse);
			}
		}

	}
}
