// MonBureau.UI/Features/Documents/ItemDialog.cs
using MonBureau.Core.Entities;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.Features.Documents
{
    public class ItemDialog : EntityDialog
    {
        public ItemDialog(CaseItem? item = null) : base(item)
        {
        }
    }
}