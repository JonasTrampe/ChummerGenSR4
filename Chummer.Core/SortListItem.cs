using System;
using System.Collections;

namespace Chummer
{
	/// <summary>
	/// Sort ListItems by Name in alphabetical order.
	/// </summary>
	public class SortListItem : IComparer
	{
		public int Compare(object objX, object objY)
		{
			ListItem lx = objX as ListItem;
			ListItem ly = objY as ListItem;

			return string.Compare(lx.Name, ly.Name);
		}
	}

}

