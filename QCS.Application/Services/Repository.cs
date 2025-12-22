
using Microsoft.EntityFrameworkCore;
using QCS.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QCS.Application.Services
{
    public interface IRepository<T> where T : class
    {
        T New();
        IQueryable<T> GetAll();
        Task<T?> GetByIdAsync(int id);

        // สำหรับเพิ่มข้อมูล
        Task AddAsync(T entity);

        // สำหรับอัปเดตข้อมูล
        Task UpdateAsync(T entity);

        // สำหรับลบข้อมูล (รองรับทั้งชื่อ RemoveAsync และ DeleteAsync เพื่อความยืดหยุ่น)
        Task RemoveAsync(T entity);
        Task DeleteAsync(T entity);
        Task DeleteRangeAsync(IEnumerable<T> entities);

        // ✅ เพิ่มเมธอดนี้เพื่อให้ GenericController เรียกใช้ได้
        Task<int> SaveChangesAsync();
    }

    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }
        public T New()
        {
            return Activator.CreateInstance<T>();
        }
        public IQueryable<T> GetAll()
        {
            return _dbSet;
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            // หมายเหตุ: ไม่ใส่ SaveChanges ที่นี่เพื่อให้ Controller ควบคุมจังหวะการบันทึกเองได้
        }

        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            await Task.CompletedTask;
        }

        public async Task RemoveAsync(T entity)
        {
            _dbSet.Remove(entity);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(T entity)
        {
            await RemoveAsync(entity);
        }

        public async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            await Task.CompletedTask;
        }

        // ✅ Implementation สำหรับการบันทึกข้อมูลแบบรวมศูนย์
        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}