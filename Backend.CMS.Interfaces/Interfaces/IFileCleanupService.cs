using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Interfaces.Interfaces
{
    public interface IFileCleanupService
    {
        Task CleanupTempFilesAsync();
        Task CleanupOldLogsAsync();
        Task OptimizeStorageAsync();
    }

}
