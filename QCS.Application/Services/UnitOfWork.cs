using Microsoft.EntityFrameworkCore.Storage;
using QCS.Application.Services;
using QCS.Infrastructure.Data;
using System;
using System.Collections;

namespace QCS.Infrastructure.Services
{
    public interface IUnitOfWork : IDisposable
    {
        // เมธอดสำหรับเรียกใช้ Repository ของ Entity ใดๆ แบบ Generic
        IRepository<T> Repository<T>() where T : class;

        // เมธอดสำหรับสั่งบันทึกข้อมูลทั้งหมดลง Database ทีเดียว
        Task<int> CommitAsync();

        IDbContextTransaction BeginTransaction();

        void ClearTrackedChanges();
    }
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

            // ถ้ายังไม่มี Repository ของ T ให้สร้างใหม่ (และแชร์ _context ตัวเดียวกัน)
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

        public async Task<int> CommitAsync()
        {
            // สั่ง SaveChanges ทีเดียวตรงนี้
            return await _context.SaveChangesAsync();
        }
        public IDbContextTransaction BeginTransaction()
        {
            return _context.Database.BeginTransaction();
        }

        public void ClearTrackedChanges()
        {
            _context.ChangeTracker.Clear();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}