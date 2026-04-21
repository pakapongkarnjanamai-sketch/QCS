using Microsoft.EntityFrameworkCore;
using QCS.Application.Abstractions;
using QCS.Infrastructure.Data;

namespace QCS.Infrastructure.Services
{
    /// <summary>
    /// EF Core-based generic repository. Concrete implementation of <see cref="IRepository{T}"/>.
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public T New() => Activator.CreateInstance<T>();

        public IQueryable<T> GetAll() => _dbSet;

        public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(T entity) => RemoveAsync(entity);

        public Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
