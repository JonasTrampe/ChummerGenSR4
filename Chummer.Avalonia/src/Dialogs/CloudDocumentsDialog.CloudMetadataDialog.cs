using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Chummer.NewUI.Dialogs;

public partial class CloudDocumentsDialog
{
    private sealed class CloudMetadataDialog : Window
    {
        public CloudMetadataDialog(string strDisplayName, string strDescription, string strImageUrl)
        {
            Width = 520;
            Height = 260;
            Title = T("Title_CloudMetadata");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            TextBox objDisplayName = new() { Text = strDisplayName };
            TextBox objDescription = new() { Text = strDescription, AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
            TextBox objImageUrl = new() { Text = strImageUrl };

            Button objOk = new() { Content = T("String_OK"), Width = 80 };
            objOk.Click += (_, _) =>
            {
                Close(new CloudMetadataDialogResult(objDisplayName.Text ?? string.Empty, objDescription.Text ?? string.Empty, objImageUrl.Text ?? string.Empty));
            };

            Button objCancel = new() { Content = T("String_Cancel"), Width = 80 };
            objCancel.Click += (_, _) => Close(null);

            Content = new Grid
            {
                Margin = new Thickness(12),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children =
                {
                    new TextBlock { Text = T("Label_CloudMetadata_DisplayName"), VerticalAlignment = VerticalAlignment.Center },
                    objDisplayName,
                    new TextBlock { Text = T("Label_CloudMetadata_Description"), VerticalAlignment = VerticalAlignment.Top },
                    objDescription,
                    new TextBlock { Text = T("Label_CloudMetadata_ImageUrl"), VerticalAlignment = VerticalAlignment.Center },
                    objImageUrl,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { objCancel, objOk }
                    }
                }
            };

            Grid.SetColumn(objDisplayName, 1);
            Grid.SetColumn(objDescription, 1);
            Grid.SetColumn(objImageUrl, 1);

            Grid.SetRow((Control)((Grid)Content).Children[1], 0);
            Grid.SetRow((Control)((Grid)Content).Children[2], 1);
            Grid.SetColumn((Control)((Grid)Content).Children[2], 0);
            Grid.SetRow((Control)((Grid)Content).Children[3], 1);
            Grid.SetColumn((Control)((Grid)Content).Children[3], 1);
            Grid.SetRow((Control)((Grid)Content).Children[4], 2);
            Grid.SetRow((Control)((Grid)Content).Children[5], 2);
            Grid.SetColumn((Control)((Grid)Content).Children[5], 1);
            Grid.SetRow((Control)((Grid)Content).Children[6], 4);
            Grid.SetColumnSpan((Control)((Grid)Content).Children[6], 2);
        }
    }

    private sealed record CloudMetadataDialogResult(string DisplayName, string Description, string ImageUrl);
}
