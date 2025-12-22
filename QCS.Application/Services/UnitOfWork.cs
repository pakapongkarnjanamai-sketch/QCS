using QCS.Application.Services;
using QCS.Infrastructure.Data;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace QCS.Infrastructure.Services
{
    public interface IUnitOfWork : IDisposable
    {
        // เมธอดสำหรับเรียกใช้ Repository ของ Entity ใดๆ แบบ Generic
        IRepository<T> Repository<T>() where T : class;

        // เมธอดสำหรับสั่งบันทึกข้อมูลทั้งหมดลง Database ทีเดียว
        Task<int> CommitAsync();
    }
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private Hashtable _repositories;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IRepository<T> Repository<T>() where T : class
        {
            if (_repositories == null) _repositories = new Hashtable();

            var type = typeof(T).Name;

            // ถ้ายังไม่มี Repository ของ T ให้สร้างใหม่ (และแชร์ _context ตัวเดียวกัน)
            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(Repository<>);
                var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T)), _context);
                _repositories.Add(type, repositoryInstance);
            }

            return (IRepository<T>)_repositories[type];
        }

        public async Task<int> CommitAsync()
        {
            // สั่ง SaveChanges ทีเดียวตรงนี้
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}