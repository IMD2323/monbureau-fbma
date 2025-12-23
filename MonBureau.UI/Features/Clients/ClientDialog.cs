using MonBureau.Core.Entities;
using MonBureau.UI.Views.Dialogs;

namespace MonBureau.UI.Features.Clients
{
    /// <summary>
    /// Compatibility wrapper for ClientDialog
    /// Redirects to EntityDialog to maintain backward compatibility
    /// </summary>
    public class ClientDialog : EntityDialog
    {
        public ClientDialog(Client? client = null) : base(client)
        {
        }
    }

    /// <summary>
    /// Compatibility wrapper for CaseDialog
    /// Redirects to EntityDialog to maintain backward compatibility
    /// </summary>
    public class CaseDialog : EntityDialog
    {
        public CaseDialog(Case? caseEntity = null) : base(caseEntity)
        {
        }
    }

    /// <summary>
    /// Compatibility wrapper for ItemDialog
    /// Redirects to EntityDialog to maintain backward compatibility
    /// </summary>
    public class ItemDialog : EntityDialog
    {
        public ItemDialog(CaseItem? item = null) : base(item)
        {
        }
    }
}