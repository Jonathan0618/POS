using POS.Data.Context;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;

namespace POS.Services.Repository
{
    public class BaseRepository<T> where T : class
    {
        private readonly POSContext _context;
        private readonly DbSet<T> _dbSet;
        public BaseRepository()
        {
            _context = new POSContext();
            _dbSet = _context.Set<T>();
        }

        public void Add(T entity)
        {
            _dbSet.Add(entity);
            _context.SaveChanges();
        }

        public void Update(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            _context.SaveChanges();
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
            _context.SaveChanges();
        }

        public T GetById(object id)
        {
            return _dbSet.Find(id);
        }

        public IEnumerable<T> GetAll()
        {
            return _dbSet.ToList();
        }

        public IEnumerable<T> Fetch(Expression<Func<T, bool>> expression)
        {
            return _dbSet.Where(expression).ToList();
        }
    }

    
}
