namespace QCS.Application.Abstractions
{
    /// <summary>
    /// Generic repository abstraction owned by the Application layer.
    /// The concrete implementation lives in the Infrastructure layer.
    /// </summary>
    public interface IRepository<T> where T : class
    {
        T New();
        IQueryable<T> GetAll();
        Task<T?> GetByIdAsync(int id);

        Task AddAsync(T entity);
        Task UpdateAsync(T entity);

        Task RemoveAsync(T entity);
        Task DeleteAsync(T entity);
        Task DeleteRangeAsync(IEnumerable<T> entities);

        Task<int> SaveChangesAsync();
    }
}
