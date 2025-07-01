using Backend.CMS.Application.Components;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IPageContentValidationService
    {
        bool ValidatePageContent(Dictionary<string, object> content);
        bool ValidatePageLayout(Dictionary<string, object> layout);
        List<string> GetValidationErrors(Dictionary<string, object> data, string section);
        bool TryDeserializeComponent(Dictionary<string, object> componentData, out BaseComponent? component);
        bool TryDeserializeBlock(Dictionary<string, object> blockData, out Block? block);
    }
}