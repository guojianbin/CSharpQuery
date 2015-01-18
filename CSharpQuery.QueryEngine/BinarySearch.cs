﻿/***** CSharpQuery **** By: Nathan Zaugg **** Created: 3/9/2009 *************
 * This software is licensed under Microsoft Public License (Ms-PL)			*
 * http://www.microsoft.com/opensource/licenses.mspx						*
 *																			*
 * Downloaded From: http://www.InteractiveASP.NET							*
 ****************************************************************************/

using System.Collections.Generic;
using CSharpQuery.Index;

namespace CSharpQuery.QueryEngine {
	public class BinarySearch<T> {
		private readonly SearchPredicateComparer searchMethod;

		public delegate int SearchPredicateComparer(string str1, string str2);

		public BinarySearch(SearchPredicateComparer searchMethod)
		{
			this.searchMethod = searchMethod;
		}

		public List<string> Search(SortedList<string, List<WordReference<T>>> value, string searchPhrase) {

			List<string> results = new List<string>();

			int index = SearchRecursive(0, value.Keys.Count, value, searchPhrase, searchMethod);

			// search my way back up the list
			int idx = (index-1) > 0 ? (index-1) : 0;
			while (idx > 0 && searchMethod(searchPhrase, value.Keys[idx]) == 0) {
				results.Add(value.Keys[idx]);
				idx--;
			}

			results.Reverse();

			idx = index;
			// then back down
			while (idx < value.Keys.Count && searchMethod(searchPhrase, value.Keys[idx]) == 0) {
				results.Add(value.Keys[idx]);
				idx++;
			}

			return results;
		}

		private static int SearchRecursive(int start, int end, SortedList<string, List<WordReference<T>>> value, string searchPhrase, SearchPredicateComparer searchMethod) {
			if (start == end || start > end || end-start==1)
				return start;

			int middle = start + ((end - start) / 2);
			
			// test the middle
			string key = value.Keys[middle];

			int result = searchMethod(searchPhrase, key);
			if (result < 0)
				return SearchRecursive(start, middle, value, searchPhrase, searchMethod);
			else if (result > 0)
				return SearchRecursive(middle, end, value, searchPhrase, searchMethod);
			else 
				return middle;
		}

	}
}
