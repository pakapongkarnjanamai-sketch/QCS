
using Microsoft.EntityFrameworkCore;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace QCS.Infrastructure.Services
{
    public interface IRepository<T> where T : class
    {
        // Sync Methods
        IQueryable<T> GetAll();
        T? GetById(int id);
        T New();
        void Add(T entity);
        void Update(T entity);
        void Remove(T entity);
        void SaveChanges();
        IQueryable<T> Find(Expression<Func<T, bool>> expression);

        // Async Methods
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task AddAsync(T entity);
        Task SaveChangesAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression);
        Task<bool> ExistsAsync(int id);
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> expression);
    }
    public class Repository<T> : IRepository<T> where T : BaseEntity, new()
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        // Sync Methods
        public virtual IQueryable<T> GetAll()
        {
            return _dbSet.AsQueryable();
        }

        public virtual T? GetById(int id)
        {
            return _dbSet.FirstOrDefault(x => x.Id == id);
        }

        public virtual T New()
        {
            return new T();
        }

        public virtual void Add(T entity)
        {
            entity.CreatedAt = DateTime.UtcNow;
            _dbSet.Add(entity);
        }

        public virtual void Update(T entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(entity);
        }

        public virtual void Remove(T entity)
        {

            _dbSet.Remove(entity);
        }

        public virtual IQueryable<T> Find(Expression<Func<T, bool>> expression)
        {
            return _dbSet.Where(expression).Where(x => x.IsActive);
        }

        public virtual void SaveChanges()
        {
            _context.SaveChanges();
        }

        // Async Methods
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.Where(x => x.IsActive).ToListAsync();
        }

        public virtual async Task AddAsync(T entity)
        {
            entity.CreatedAt = DateTime.UtcNow;
            await _dbSet.AddAsync(entity);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.Where(expression).Where(x => x.IsActive).ToListAsync();
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            return await _dbSet.AnyAsync(x => x.Id == id && x.IsActive);
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync(x => x.IsActive);
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.Where(expression).CountAsync(x => x.IsActive);
        }

        public virtual async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
