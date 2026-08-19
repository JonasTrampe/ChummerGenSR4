using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public partial class CharacterFileServiceTests
{
    [Fact]
    public void CharacterClipboard_CopiesImmutableCollectionItemsIntoMatchingTargets()
    {
        CharacterDocument source = LoadXml("<character><gears><gear><name>Toolkit</name><category>Tools</category>"
            + "<qty>1</qty></gear></gears></character>");
        CharacterDocument target = LoadXml("<character><gears /></character>");
        var clipboard = new CharacterClipboard();

        Assert.True(clipboard.Copy(source, "/character/gears/gear", ClipboardContentType.Gear));
        Assert.False(clipboard.Paste(target, "/character/gears", ClipboardContentType.Weapon));
        Assert.True(clipboard.Paste(target, "/character/gears", ClipboardContentType.Gear));
        Assert.Equal("Toolkit", target.Document.SelectSingleNode("/character/gears/gear/name")!.InnerText);
        source.Document.SelectSingleNode("/character/gears/gear/name")!.InnerText = "Changed";
        Assert.Equal("Toolkit", target.Document.SelectSingleNode("/character/gears/gear/name")!.InnerText);
    }

}
