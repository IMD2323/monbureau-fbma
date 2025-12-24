using MonBureau.Core.Entities;
using MonBureau.UI.Views.Dialogs;
using MonBureau.UI.Features.Clients;

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
}