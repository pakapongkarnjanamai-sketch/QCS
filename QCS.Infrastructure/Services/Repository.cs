using Microsoft.EntityFrameworkCore;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Linq.Expressions;

namespace QCS.Infrastructure.Services
{
    public interface IRepository<T> where T : class
    {
        // Sync Methods (เก็บไว้เพราะ DevExtreme LoadOptions บางทีต้องการ IQueryable)
        IQueryable<T> GetAll();
        T? GetById(int id);
        T New();

        // ควรใช้ Async สำหรับ Write Operations
        Task AddAsync(T entity);
        Task UpdateAsync(T entity); // เพิ่ม Async
        Task RemoveAsync(T entity); // เปลี่ยนจาก Void เป็น Task

        // Read Methods Async
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression);
        Task<bool> ExistsAsync(int id);
        Task SaveChangesAsync();
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

        // --- Sync Methods (Optimized for Queryable/DevExtreme) ---
        public virtual IQueryable<T> GetAll()
        {
            // แก้ไข: กรอง IsActive ให้เหมือนกับ Async Method
            return _dbSet.Where(x => x.IsActive);
        }

        public virtual T? GetById(int id)
        {
            return _dbSet.FirstOrDefault(x => x.Id == id && x.IsActive);
        }

        public virtual T New()
        {
            return new T();
        }

        // --- Async Write Methods (Recommended) ---
        public virtual async Task AddAsync(T entity)
        {
            entity.CreatedAt = DateTime.UtcNow;
            entity.IsActive = true; // มั่นใจว่าสร้างใหม่ต้อง Active
            await _dbSet.AddAsync(entity);
        }

        public virtual Task UpdateAsync(T entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual Task RemoveAsync(T entity)
        {
            // Soft Delete (ถ้า BaseEntity ออกแบบมาเพื่อ Soft Delete ควรแก้ IsActive = false แทนการ Remove)
            // แต่ถ้าเป็นการลบจริงใช้บรรทัดนี้:
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public virtual async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // --- Async Read Methods ---
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.Where(x => x.IsActive).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.Where(expression).Where(x => x.IsActive).ToListAsync();
        }

        public virtual async Task<bool> ExistsAsync(int id)
        {
            return await _dbSet.AnyAsync(x => x.Id == id && x.IsActive);
        }
    }
}