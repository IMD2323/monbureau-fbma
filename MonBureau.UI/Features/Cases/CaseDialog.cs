// MonBureau.UI/Features/Cases/CaseDialog.cs
using MonBureau.Core.Entities;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.Features.Cases
{
    public class CaseDialog : EntityDialog
    {
        public CaseDialog(Case? caseEntity = null) : base(caseEntity)
        {
        }
    }
}