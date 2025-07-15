using Backend.CMS.Application.DTOs;
using Frontend.Components.Common.ConfirmationDialogComponent;
using Frontend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
namespace Frontend.Components.Common.GenericCrudPage
{
    public partial class GenericCrudPage<TListItem, TDetailItem, TCreateDto, TUpdateDto> : ComponentBase
    {

        public enum ViewMode { Grid, List }

        // Parameters
        [Parameter] public string PageTitle { get; set; } = string.Empty;
        [Parameter] public string EntitySingularName { get; set; } = "Item";
        [Parameter] public string EntityPluralName { get; set; } = "Items";
        [Parameter] public string PageDescription { get; set; } = string.Empty;
        [Parameter] public string HeaderIcon { get; set; } = "fas fa-list";
        [Parameter] public string DialogSize { get; set; } = "large";
        [Parameter] public bool ShowDuplicateAction { get; set; } = false;
        [Parameter] public bool ShowViewModeToggle { get; set; } = false;
        [Parameter] public string EmptyStateIcon { get; set; } = "fas fa-inbox";
        [Parameter] public string EmptyStateTitle { get; set; } = "No items found";
        [Parameter] public string EmptyStateMessage { get; set; } = "Get started by creating your first item.";

        // Content Parameters
        [Parameter] public RenderFragment? TableColumns { get; set; }
        [Parameter] public RenderFragment<TListItem>? RowTemplate { get; set; }
        [Parameter] public RenderFragment<TListItem>? CustomRowActions { get; set; }
        [Parameter] public RenderFragment<(object Model, Dictionary<string, string> ValidationErrors, bool IsEditMode)>? FormContent { get; set; }
        [Parameter] public RenderFragment<IEnumerable<TListItem>>? GridContent { get; set; }
        [Parameter] public RenderFragment? FilterContent { get; set; }
        [Parameter] public RenderFragment? CustomHeaderActions { get; set; }

        // Data Parameters
        [Parameter] public Func<TListItem, int> IdSelector { get; set; } = null!;
        [Parameter] public Func<TCreateDto> CreateModelFactory { get; set; } = null!;
        [Parameter] public Func<TDetailItem, TCreateDto> EditModelFactory { get; set; } = null!;

        // Service Parameters
        [Parameter] public Func<int, int, string?, Task<PaginatedResult<TListItem>>> LoadDataFunc { get; set; } = null!;
        [Parameter] public Func<int, Task<TDetailItem?>> GetByIdFunc { get; set; } = null!;
        [Parameter] public Func<TCreateDto, Task<TDetailItem?>> CreateFunc { get; set; } = null!;
        [Parameter] public Func<int, TUpdateDto, Task<TDetailItem?>> UpdateFunc { get; set; } = null!;
        [Parameter] public Func<int, Task<bool>> DeleteFunc { get; set; } = null!;
        [Parameter] public Func<int, string, Task<TDetailItem?>>? DuplicateFunc { get; set; }
        [Parameter] public Func<TCreateDto, TUpdateDto> CreateToUpdateMapper { get; set; } = null!;

        // Validation Parameters
        [Parameter] public Func<TCreateDto, bool, Task<Dictionary<string, string>>> ValidateFunc { get; set; } = (model, isEdit) => Task.FromResult(new Dictionary<string, string>());

        // Events
        [Parameter] public EventCallback<TDetailItem> OnEntityCreated { get; set; }
        [Parameter] public EventCallback<TDetailItem> OnEntityUpdated { get; set; }
        [Parameter] public EventCallback<int> OnEntityDeleted { get; set; }
        [Parameter] public EventCallback<int> OnView { get; set; }
        [Parameter] public EventCallback<int> OnDuplicate { get; set; }

        // Private fields
        private PaginatedResult<TListItem> pagedResult = new();
        private bool isLoading = true;
        private bool isSaving = false;
        private int currentPage = 1;
        private int pageSize = 10;
        private int totalPages = 0;
        private string searchTerm = string.Empty;
        private Timer? searchTimer;
        private Dictionary<string, string> validationErrors = new();
        private ViewMode currentViewMode = ViewMode.List;

        // Dialog states
        private bool showEntityDialog = false;
        private bool showDuplicateDialog = false;
        private string dialogTitle = string.Empty;
        private string dialogDescription = string.Empty;
        private bool isEditMode = false;
        private int selectedEntityId = 0;
        private string duplicateName = string.Empty;

        // Form models
        private TCreateDto entityModel = default!;
        private readonly object duplicateModel = new();

        // Component references
        private FormDialog? entityDialog;
        private FormDialog? duplicateDialog;
        private ConfirmationDialog? deleteDialog;

        protected override async Task OnInitializedAsync()
        {
            entityModel = CreateModelFactory();
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                isLoading = true;
                StateHasChanged();

                pagedResult = await LoadDataFunc(currentPage, pageSize, string.IsNullOrEmpty(searchTerm) ? null : searchTerm);
                totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize);
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

        private void OnSearchKeyUp(KeyboardEventArgs e)
        {
            searchTimer?.Dispose();
            searchTimer = new Timer(async _ =>
            {
                currentPage = 1;
                await InvokeAsync(LoadData);
            }, null, 500, Timeout.Infinite);
        }

        private async Task OnPageChanged(int page)
        {
            currentPage = page;
            await LoadData();
        }

        private async Task RefreshData()
        {
            await LoadData();
        }

        private void SetViewMode(ViewMode mode)
        {
            currentViewMode = mode;
            StateHasChanged();
        }

        // Dialog Management
        private void ShowCreateDialog()
        {
            isEditMode = false;
            dialogTitle = $"Create New {EntitySingularName}";
            dialogDescription = $"Add a new {EntitySingularName.ToLower()} to the system";
            entityModel = CreateModelFactory();
            validationErrors.Clear();
            showEntityDialog = true;
            StateHasChanged();
        }

        private async Task ShowEditDialog(int entityId)
        {
            try
            {
                isEditMode = true;
                selectedEntityId = entityId;
                dialogTitle = $"Edit {EntitySingularName}";
                dialogDescription = $"Update the {EntitySingularName.ToLower()} information";

                var entity = await GetByIdFunc(entityId);
                if (entity != null)
                {
                    entityModel = EditModelFactory(entity);
                    validationErrors.Clear();
                    showEntityDialog = true;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load {EntitySingularName.ToLower()}: {ex.Message}");
            }
        }

        private void CloseEntityDialog()
        {
            showEntityDialog = false;
            entityModel = CreateModelFactory();
            validationErrors.Clear();
            selectedEntityId = 0;
            StateHasChanged();
        }

        private async Task SaveEntity()
        {
            if (isSaving) return;

            try
            {
                isSaving = true;
                StateHasChanged();

                // Validate
                validationErrors = await ValidateFunc(entityModel, isEditMode);
                if (validationErrors.Count != 0)
                {
                    StateHasChanged();
                    return;
                }

                if (isEditMode)
                {
                    var updateDto = CreateToUpdateMapper(entityModel);
                    var result = await UpdateFunc(selectedEntityId, updateDto);
                    if (result != null)
                    {
                        NotificationService.ShowSuccess($"{EntitySingularName} updated successfully");
                        await OnEntityUpdated.InvokeAsync(result);
                    }
                }
                else
                {
                    var result = await CreateFunc(entityModel);
                    if (result != null)
                    {
                        NotificationService.ShowSuccess($"{EntitySingularName} created successfully");
                        await OnEntityCreated.InvokeAsync(result);
                    }
                }

                CloseEntityDialog();
                await LoadData();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to save {EntitySingularName.ToLower()}: {ex.Message}");
            }
            finally
            {
                isSaving = false;
                StateHasChanged();
            }
        }

        // Actions
        private void ShowDeleteConfirmation(int entityId)
        {
            selectedEntityId = entityId;
            deleteDialog?.Show();
        }

        private async Task DeleteEntity()
        {
            try
            {
                // Ensure dialog is properly closed first
                deleteDialog?.Hide();

                var success = await DeleteFunc(selectedEntityId);
                if (success)
                {
                    NotificationService.ShowSuccess($"{EntitySingularName} deleted successfully");
                    await OnEntityDeleted.InvokeAsync(selectedEntityId);

                    // Reset selection
                    selectedEntityId = 0;

                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to delete {EntitySingularName.ToLower()}: {ex.Message}");
            }
            finally
            {
                // Force state update to ensure UI is refreshed
                StateHasChanged();
            }
        }

        private void ShowDuplicateDialog(int entityId)
        {
            if (!ShowDuplicateAction || DuplicateFunc == null) return;

            selectedEntityId = entityId;
            duplicateName = string.Empty;
            showDuplicateDialog = true;
            StateHasChanged();
        }

        private void CloseDuplicateDialog()
        {
            showDuplicateDialog = false;
            duplicateName = string.Empty;
            selectedEntityId = 0;
            StateHasChanged();
        }

        private async Task DuplicateEntity()
        {
            if (string.IsNullOrEmpty(duplicateName) || DuplicateFunc == null) return;

            try
            {
                isSaving = true;
                StateHasChanged();

                var result = await DuplicateFunc(selectedEntityId, duplicateName);
                if (result != null)
                {
                    NotificationService.ShowSuccess($"{EntitySingularName} duplicated successfully");
                    CloseDuplicateDialog();
                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to duplicate {EntitySingularName.ToLower()}: {ex.Message}");
            }
            finally
            {
                isSaving = false;
                StateHasChanged();
            }
        }

        private int GetItemId(TListItem item)
        {
            if (IdSelector != null)
            {
                return IdSelector(item);
            }

            // Try to get Id property using reflection
            var idProperty = typeof(TListItem).GetProperty("Id");
            if (idProperty != null && idProperty.PropertyType == typeof(int))
            {
                return (int)(idProperty.GetValue(item) ?? 0);
            }

            throw new InvalidOperationException($"Cannot determine ID for item of type {typeof(TListItem).Name}. Please provide an IdSelector.");
        }

        public void Dispose()
        {
            searchTimer?.Dispose();
        }
    }
}

