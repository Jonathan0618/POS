using POS.Data.Context;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;

namespace POS.Services.Repository
{
    public class BaseRepository<T, TKey> : IDisposable where T : class
    {
        private readonly POSContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly bool _ownsContext;
        private readonly bool _autoSave;

        public BaseRepository()
            : this(new POSContext(), true, true)
        {
        }

        public BaseRepository(POSContext context, bool autoSave = true)
            : this(context, false, autoSave)
        {
        }

        private BaseRepository(POSContext context, bool ownsContext, bool autoSave)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            _context = context;
            _dbSet = _context.Set<T>();
            _ownsContext = ownsContext;
            _autoSave = autoSave;
        }

        public void Add(T entity)
        {
            _dbSet.Add(entity);
            SaveIfRequired();
        }

        public void Update(T entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
                _dbSet.Attach(entity);

            _context.Entry(entity).State = EntityState.Modified;
            SaveIfRequired();
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
            SaveIfRequired();
        }

        public T GetById(TKey id)
        {
            return _dbSet.Find(id);
        }

        public IEnumerable<T> GetAll()
        {
            return _dbSet.ToList();
        }

        public IQueryable<T> GetAllAsQueryable()
        {
            return _dbSet.AsNoTracking();
        }

        public IEnumerable<T> Fetch(Expression<Func<T, bool>> expression)
        {
            return _dbSet.Where(expression).ToList();
        }

        private void SaveIfRequired()
        {
            if (_autoSave)
                _context.SaveChanges();
        }

        public void Dispose()
        {
            if (_ownsContext)
                _context.Dispose();
        }
    }

    
}
