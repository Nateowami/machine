using System.Collections.Generic;
using System.Text;

namespace SIL.Machine.Translation
{
	public enum AlignmentType
	{
		Unknown = -1,
		NotAligned = 0,
		Aligned = 1
	}

	public class WordAlignmentMatrix
	{
		private readonly AlignmentType[,] _matrix;

		public WordAlignmentMatrix(int i, int j, AlignmentType defaultValue = AlignmentType.NotAligned)
		{
			_matrix = new AlignmentType[i, j];
			if (defaultValue != AlignmentType.NotAligned)
				SetAll(defaultValue);
		}

		public int RowCount => _matrix.GetLength(0);

		public int ColumnCount => _matrix.GetLength(1);

		public void SetAll(AlignmentType value)
		{
			for (int i = 0; i < RowCount; i++)
			{
				for (int j = 0; j < ColumnCount; j++)
					_matrix[i, j] = value;
			}
		}

		public AlignmentType this[int i, int j]
		{
			get { return _matrix[i, j]; }
			set { _matrix[i, j] = value; }
		}

		public AlignmentType IsRowWordAligned(int i)
		{
			for (int j = 0; j < ColumnCount; j++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					return AlignmentType.Aligned;
				if (_matrix[i, j] == AlignmentType.Unknown)
					return AlignmentType.Unknown;
			}
			return AlignmentType.NotAligned;
		}

		public AlignmentType IsColumnWordAligned(int j)
		{
			for (int i = 0; i < RowCount; i++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					return AlignmentType.Aligned;
				if (_matrix[i, j] == AlignmentType.Unknown)
					return AlignmentType.Unknown;
			}
			return AlignmentType.NotAligned;
		}

		public IEnumerable<int> GetRowWordAlignedIndices(int i)
		{
			for (int j = 0; j < ColumnCount; j++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					yield return j;
			}
		}

		public IEnumerable<int> GetColumnWordAlignedIndices(int j)
		{
			for (int i = 0; i < RowCount; i++)
			{
				if (_matrix[i, j] == AlignmentType.Aligned)
					yield return i;
			}
		}

		public string ToGizaFormat(IEnumerable<string> sourceSegment, IEnumerable<string> targetSegment)
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{0}\n", string.Join(" ", targetSegment));

			var sourceWords = new List<string> {"NULL"};
			sourceWords.AddRange(sourceSegment);

			int i = 0;
			foreach (string sourceWord in sourceWords)
			{
				if (i > 0)
					sb.Append(" ");
				sb.Append(sourceWord);
				sb.Append(" ({ ");
				for (int j = 0; j < ColumnCount; j++)
				{
					if (i == 0)
					{
						if (IsColumnWordAligned(j) == AlignmentType.NotAligned)
						{
							sb.Append(j + 1);
							sb.Append(" ");
						}
					}
					else if (_matrix[i - 1, j] == AlignmentType.Aligned)
					{
						sb.Append(j + 1);
						sb.Append(" ");
					}
				}

				sb.Append("})");
				i++;
			}
			sb.Append("\n");
			return sb.ToString();
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			for (int i = RowCount - 1; i >= 0; i--)
			{
				for (int j = 0; j < ColumnCount; j++)
				{
					if (_matrix[i, j] == AlignmentType.Unknown)
						sb.Append("U");
					else
						sb.Append((int) _matrix[i, j]);
					sb.Append(" ");
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}
	}
}
