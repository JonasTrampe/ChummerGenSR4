using System;
using System.Collections;

namespace Chummer.Core
{
    /// <summary>
    ///     Sort ListItems by Name in alphabetical order.
    /// </summary>
    public class SortListItem : IComparer
    {
        public int Compare(object? objX, object? objY)
        {
            var lx = objX as ListItem;
            var ly = objY as ListItem;

            return string.CompareOrdinal(lx?.Name, ly?.Name);
        }
    }
}