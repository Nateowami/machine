﻿using System.Collections.Generic;
using System.Linq;

namespace SIL.Machine.Tokenization
{
	public static class TokenizationExtensions
	{
		public static IEnumerable<string> TokenizeToStrings(this ITokenizer<string, int> tokenizer, string str)
		{
			return tokenizer.Tokenize(str).Select(range => str.Substring(range.Start, range.Length));
		}
	}
}
