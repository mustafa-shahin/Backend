using Backend.CMS.Application.DTOs;
using Frontend.Components.Common;
using Frontend.Components.Common.ConfirmationDialogComponent;
using Microsoft.AspNetCore.Components;

namespace Frontend.Components.Common.ObjectSelector
{
    public partial class ObjectSelector<TEntity> : ComponentBase, IDisposable where TEntity : class
    {
        // Private fields
        private List<TEntity> selectedEntities = new();
        private bool isLoading = false;
        private bool isProcessing = false;
        private bool showEntityCount = true;

        // Dialog references
        private FormDialog? entityBrowserDialog;
        private ConfirmationDialog? removeEntityDialog;
        private ConfirmationDialog? clearAllDialog;

        // Entity browser state
        private bool showEntityBrowserDialog = false;
        private List<TEntity> selectedBrowserEntities = new();

        // Entity removal state
        private TEntity? entityToRemove = null;

        // Parameter change tracking
        private bool hasInitialized = false;
        private readonly SemaphoreSlim loadingSemaphore = new(1, 1);

        // State tracking for immediate updates
        private bool isInternalUpdate = false;

        protected override async Task OnInitializedAsync()
        {
            // Validate required parameters
            ValidateParameters();

            // Initialize from parameters
            InitializeFromParameters();

            hasInitialized = true;

            // Load initial data if needed
            await LoadInitialDataAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!hasInitialized) return;

            // Skip if this is an internal update to prevent loops
            if (isInternalUpdate)
            {
                isInternalUpdate = false;
                return;
            }

            var parametersChanged = false;

            // Handle selected entities parameter changes
            if (!AreEntityListsEqual(SelectedEntities, selectedEntities))
            {
                selectedEntities = SelectedEntities?.ToList() ?? new List<TEntity>();
                parametersChanged = true;
            }

            if (parametersChanged)
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        private void ValidateParameters()
        {
            if (GetEntityId == null)
                throw new ArgumentNullException(nameof(GetEntityId), "GetEntityId function is required");

            if (GetEntityDisplayName == null)
                throw new ArgumentNullException(nameof(GetEntityDisplayName), "GetEntityDisplayName function is required");

            if (LoadEntitiesFunc == null)
                throw new ArgumentNullException(nameof(LoadEntitiesFunc), "LoadEntitiesFunc is required");
        }

        private void InitializeFromParameters()
        {
            // Initialize selected entities if provided
            if (SelectedEntities?.Any() == true)
            {
                selectedEntities = SelectedEntities.ToList();
            }

            // Set initial state
            isLoading = false;
        }

        private bool AreEntityListsEqual(List<TEntity>? list1, List<TEntity> list2)
        {
            if (list1 == null && list2.Count == 0) return true;
            if (list1 == null || list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (GetEntityId(list1[i]) != GetEntityId(list2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        #region Entity Management

        private async Task LoadInitialDataAsync()
        {
            if (!await loadingSemaphore.WaitAsync(100))
            {
                return;
            }

            try
            {
                isLoading = true;
                await InvokeAsync(StateHasChanged);

                // Initial data is already loaded from parameters
                selectedEntities = SelectedEntities?.ToList() ?? new List<TEntity>();
                await NotifyEntitiesChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load {EntityPluralName.ToLower()}: {ex.Message}");
                selectedEntities.Clear();
            }
            finally
            {
                isLoading = false;
                loadingSemaphore.Release();
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task AddEntity(TEntity entity)
        {
            // Check entity count limit
            if (selectedEntities.Count >= MaxEntities)
            {
                NotificationService.ShowError($"Cannot add more than {MaxEntities} {EntityPluralName.ToLower()}.");
                return;
            }

            // Check for duplicates
            var entityId = GetEntityId(entity);
            if (selectedEntities.Any(e => GetEntityId(e) == entityId))
            {
                NotificationService.ShowError($"{EntitySingularName} '{GetEntityDisplayName(entity)}' is already selected.");
                return;
            }

            selectedEntities.Add(entity);
            await NotifyEntitiesChanged();
            await NotifyEntityAdded(entity);
            await InvokeAsync(StateHasChanged);
        }

        private async Task RemoveEntity(TEntity entity)
        {
            var entityId = GetEntityId(entity);
            var removed = selectedEntities.RemoveAll(e => GetEntityId(e) == entityId) > 0;

            if (removed)
            {
                await NotifyEntitiesChanged();
                await NotifyEntityRemoved(entity);
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ClearEntities()
        {
            var removedEntities = selectedEntities.ToList();
            selectedEntities.Clear();

            await NotifyEntitiesChanged();

            // Notify for each removed entity
            foreach (var entity in removedEntities)
            {
                await NotifyEntityRemoved(entity);
            }

            await InvokeAsync(StateHasChanged);
        }

        #endregion

        #region Event Notifications

        private async Task NotifyEntitiesChanged()
        {
            isInternalUpdate = true;
            if (OnEntitiesChanged.HasDelegate)
            {
                await OnEntitiesChanged.InvokeAsync(selectedEntities.ToList());
            }
        }

        private async Task NotifyEntityAdded(TEntity entity)
        {
            if (OnEntityAdded.HasDelegate)
            {
                await OnEntityAdded.InvokeAsync(entity);
            }
        }

        private async Task NotifyEntityRemoved(TEntity entity)
        {
            if (OnEntityRemoved.HasDelegate)
            {
                await OnEntityRemoved.InvokeAsync(entity);
            }
        }

        #endregion

        #region Entity Browser

        private void OpenEntityBrowser()
        {
            showEntityBrowserDialog = true;
            selectedBrowserEntities.Clear();
        }

        private void CloseEntityBrowser()
        {
            showEntityBrowserDialog = false;
            selectedBrowserEntities.Clear();
        }

        private void OnEntityBrowserSelectionChanged(List<TEntity> selectedEntities)
        {
            selectedBrowserEntities = selectedEntities;
        }

        private async Task AddSelectedBrowserEntities()
        {
            if (!selectedBrowserEntities.Any())
            {
                CloseEntityBrowser();
                return;
            }

            try
            {
                isProcessing = true;
                await InvokeAsync(StateHasChanged);

                var addedCount = 0;
                foreach (var entity in selectedBrowserEntities)
                {
                    var entityId = GetEntityId(entity);
                    if (selectedEntities.Any(e => GetEntityId(e) == entityId))
                        continue;

                    if (selectedEntities.Count >= MaxEntities)
                    {
                        NotificationService.ShowError($"Cannot add more {EntityPluralName.ToLower()}. Maximum {MaxEntities} allowed.");
                        break;
                    }

                    await AddEntity(entity);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    NotificationService.ShowSuccess($"Added {addedCount} {(addedCount == 1 ? EntitySingularName.ToLower() : EntityPluralName.ToLower())}");
                }

                CloseEntityBrowser();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add {EntityPluralName.ToLower()}: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private List<int> GetExcludedEntityIds()
        {
            return selectedEntities.Select(GetEntityId).ToList();
        }

        #endregion

        #region Entity Actions

        private async Task PreviewEntity(TEntity entity)
        {
            try
            {
                if (OnEntityPreview.HasDelegate)
                {
                    await OnEntityPreview.InvokeAsync(entity);
                }
                else
                {
                    NotificationService.ShowInfo($"Preview for {GetEntityDisplayName(entity)}");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to preview {EntitySingularName.ToLower()}: {ex.Message}");
            }
        }

        private void ShowRemoveConfirmation(TEntity entity)
        {
            entityToRemove = entity;
            removeEntityDialog?.Show();
        }

        private async Task RemoveSelectedEntity()
        {
            if (entityToRemove == null) return;

            try
            {
                isProcessing = true;
                await InvokeAsync(StateHasChanged);

                await RemoveEntity(entityToRemove);
                NotificationService.ShowSuccess($"Removed {GetEntityDisplayName(entityToRemove)}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to remove {EntitySingularName.ToLower()}: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                entityToRemove = null;
                await InvokeAsync(StateHasChanged);
            }
        }

        private void ShowClearConfirmation()
        {
            clearAllDialog?.Show();
        }

        private async Task ClearAllEntities()
        {
            try
            {
                isProcessing = true;
                await InvokeAsync(StateHasChanged);

                var count = selectedEntities.Count;
                await ClearEntities();
                NotificationService.ShowSuccess($"Cleared {count} {(count == 1 ? EntitySingularName.ToLower() : EntityPluralName.ToLower())}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to clear {EntityPluralName.ToLower()}: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        #endregion

        #region Helper Methods

        private string GetGridClasses()
        {
            return GridSize.ToLower() switch
            {
                "small" => "grid-cols-2 sm:grid-cols-4 md:grid-cols-6 lg:grid-cols-8",
                "medium" => "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6",
                "large" => "grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4",
                _ => "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6"
            };
        }

        private string GetEntityCardClasses()
        {
            return "border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden bg-white dark:bg-gray-800 shadow-sm hover:shadow-md transition-shadow duration-200";
        }

        private string GetEntityListItemClasses()
        {
            return "bg-gray-50 dark:bg-gray-700";
        }

        private string GetEntityPreviewClasses()
        {
            return GridSize.ToLower() switch
            {
                "small" => "aspect-square relative",
                "medium" => "aspect-square relative",
                "large" => "aspect-video relative",
                _ => "aspect-square relative"
            };
        }

        private string GetEntityIconBackgroundClass()
        {
            return "bg-blue-500";
        }

        #endregion

        #region Public API Methods

        public List<TEntity> GetSelectedEntities()
        {
            return selectedEntities.ToList();
        }

        public int GetSelectedEntityCount()
        {
            return selectedEntities.Count;
        }

        public bool IsMaxEntitiesReached()
        {
            return selectedEntities.Count >= MaxEntities;
        }

        public async Task AddEntityById(int entityId)
        {
            try
            {
                // This would require a GetByIdFunc parameter to be added
                // For now, entities should be added through the browser
                NotificationService.ShowInfo($"Use the browser to add {EntityPluralName.ToLower()}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add {EntitySingularName.ToLower()}: {ex.Message}");
            }
        }

        public async Task Refresh()
        {
            await LoadInitialDataAsync();
        }

        public async Task Clear()
        {
            await ClearAllEntities();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            loadingSemaphore?.Dispose();
        }

        #endregion
    }
}