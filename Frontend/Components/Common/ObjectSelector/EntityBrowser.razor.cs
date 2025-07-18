using Backend.CMS.Application.DTOs;
using Microsoft.AspNetCore.Components;

namespace Frontend.Components.Common.ObjectSelector
{
    public partial class EntityBrowser<TEntity> where TEntity : class
    {
        [Parameter] public bool AllowMultiSelect { get; set; } = true;
        [Parameter] public string EntitySingularName { get; set; } = "Item";
        [Parameter] public string EntityPluralName { get; set; } = "Items";
        [Parameter] public List<int> ExcludeEntityIds { get; set; } = new();
        [Parameter] public EventCallback<List<TEntity>> OnEntitiesSelected { get; set; }

        // Entity property accessors
        [Parameter] public Func<TEntity, int> GetEntityId { get; set; } = null!;
        [Parameter] public Func<TEntity, string> GetEntityDisplayName { get; set; } = null!;
        [Parameter] public Func<TEntity, string>? GetEntitySubtitle { get; set; }

        // Display customization
        [Parameter] public Func<string> GetEntityIcon { get; set; } = () => "fas fa-cube";
        [Parameter] public Func<string> GetEntityIconColor { get; set; } = () => "text-blue-500";

        // Data loading functions
        [Parameter] public Func<int, int, string?, Task<PaginatedResult<TEntity>>> LoadEntitiesFunc { get; set; } = null!;
        [Parameter] public Func<string, Task<List<TEntity>>>? SearchFunc { get; set; }

        private List<TEntity> entities = new();
        private List<TEntity> selectedEntities = new();
        private bool isLoading = true;
        private string searchTerm = string.Empty;
        private Timer? searchTimer;

        private int currentPage = 1;
        private int pageSize = 24;
        private int totalCount = 0;
        private int totalPages = 0;

        protected override async Task OnInitializedAsync()
        {
            await LoadEntities();
        }

        private async Task LoadEntities()
        {
            try
            {
                isLoading = true;
                StateHasChanged();

                var result = await LoadEntitiesFunc(currentPage, pageSize, string.IsNullOrEmpty(searchTerm) ? null : searchTerm);

                entities = result.Data?.ToList() ?? new List<TEntity>();
                totalCount = result.TotalCount;
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load {EntityPluralName.ToLower()}: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void OnSearchKeyUp(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
        {
            searchTimer?.Dispose();
            searchTimer = new Timer(async _ =>
            {
                currentPage = 1;
                await InvokeAsync(LoadEntities);
            }, null, 500, Timeout.Infinite);
        }

        private async Task GoToPage(int page)
        {
            if (page >= 1 && page <= totalPages)
            {
                currentPage = page;
                await LoadEntities();
            }
        }

        private async Task ToggleEntitySelection(TEntity entity)
        {
            var entityId = GetEntityId(entity);

            // Don't allow selection of excluded entities
            if (ExcludeEntityIds.Contains(entityId))
            {
                return;
            }

            if (selectedEntities.Any(e => GetEntityId(e) == entityId))
            {
                selectedEntities.RemoveAll(e => GetEntityId(e) == entityId);
            }
            else
            {
                if (!AllowMultiSelect)
                {
                    selectedEntities.Clear();
                }
                selectedEntities.Add(entity);
            }

            await OnEntitiesSelected.InvokeAsync(selectedEntities.ToList());
            StateHasChanged();
        }

        private async Task ClearSelection()
        {
            selectedEntities.Clear();
            await OnEntitiesSelected.InvokeAsync(selectedEntities.ToList());
            StateHasChanged();
        }

        private string GetEntityCardClass(bool isSelected, bool isExcluded)
        {
            var baseClass = "p-3 rounded-lg border-2 transition-all duration-200";

            if (isExcluded)
            {
                return $"{baseClass} border-gray-300 dark:border-gray-600 bg-gray-100 dark:bg-gray-700";
            }

            if (isSelected)
            {
                return $"{baseClass} border-blue-500 bg-blue-50 dark:bg-blue-900/20";
            }

            return $"{baseClass} border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-600 bg-white dark:bg-gray-800";
        }

        public void Dispose()
        {
            searchTimer?.Dispose();
        }
    }
}