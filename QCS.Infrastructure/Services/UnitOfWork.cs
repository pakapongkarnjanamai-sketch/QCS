using System.Collections;
using Microsoft.EntityFrameworkCore.Storage;
using QCS.Application.Abstractions;
using QCS.Infrastructure.Data;

namespace QCS.Infrastructure.Services
{
    /// <summary>
    /// EF Core-based unit of work. Concrete implementation of <see cref="IUnitOfWork"/>.
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private Hashtable? _repositories;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IRepository<T> Repository<T>() where T : class
        {
            _repositories ??= new Hashtable();

            var type = typeof(T).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(Repository<>);
                var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _context);
                _repositories.Add(type, repositoryInstance);
            }

            if (_repositories[type] is IRepository<T> repository)
            {
                return repository;
            }

            throw new InvalidOperationException($"Repository for type '{type}' could not be created.");
        }

        public async Task<int> CommitAsync() => await _context.SaveChangesAsync();

        public IDbContextTransaction BeginTransaction() => _context.Database.BeginTransaction();

        public void ClearTrackedChanges() => _context.ChangeTracker.Clear();

        public void Dispose() => _context.Dispose();
    }
}
