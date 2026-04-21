using Microsoft.EntityFrameworkCore.Storage;

namespace QCS.Application.Abstractions
{
    /// <summary>
    /// Unit of Work abstraction owned by the Application layer.
    /// The concrete implementation lives in the Infrastructure layer.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class;

        Task<int> CommitAsync();

        IDbContextTransaction BeginTransaction();

        void ClearTrackedChanges();
    }
}
