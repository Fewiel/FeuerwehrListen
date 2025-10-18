using Microsoft.AspNetCore.Components;

namespace FeuerwehrListen.Services
{
    public class SidebarService
    {
        private bool _isCollapsed = false;
        
        public bool IsCollapsed => _isCollapsed;
        
        public event Action? OnStateChanged;
        
        public void ToggleCollapse()
        {
            _isCollapsed = !_isCollapsed;
            NotifyStateChanged();
        }
        
        public void SetCollapsed(bool collapsed)
        {
            if (_isCollapsed != collapsed)
            {
                _isCollapsed = collapsed;
                NotifyStateChanged();
            }
        }
        
        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
