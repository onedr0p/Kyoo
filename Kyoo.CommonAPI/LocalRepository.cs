using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Kyoo.CommonApi;
using Kyoo.Models;
using Kyoo.Models.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Kyoo.Controllers
{
	public abstract class LocalRepository<T>
		where T : class, IResource
	{
		protected readonly DbContext Database;

		protected abstract Expression<Func<T, object>> DefaultSort { get; }
		
		
		protected LocalRepository(DbContext database)
		{
			Database = database;
		}
		
		public virtual void Dispose()
		{
			Database.Dispose();
		}

		public virtual ValueTask DisposeAsync()
		{
			return Database.DisposeAsync();
		}
		
		public virtual Task<T> Get(int id)
		{
			return Database.Set<T>().FirstOrDefaultAsync(x => x.ID == id);
		}

		public virtual Task<T> Get(string slug)
		{
			return Database.Set<T>().FirstOrDefaultAsync(x => x.Slug == slug);
		}
		
		public virtual Task<ICollection<T>> GetAll(Expression<Func<T, bool>> where = null,
			Sort<T> sort = default,
			Pagination limit = default)
		{
			return ApplyFilters(Database.Set<T>(), where, sort, limit);
		}
		
		protected Task<ICollection<T>> ApplyFilters(IQueryable<T> query,
			Expression<Func<T, bool>> where = null,
			Sort<T> sort = default, 
			Pagination limit = default)
		{
			return ApplyFilters(query, Get, DefaultSort, where, sort, limit);
		}
		
		protected async Task<ICollection<TValue>> ApplyFilters<TValue>(IQueryable<TValue> query,
			Func<int, Task<TValue>> get,
			Expression<Func<TValue, object>> defaultSort,
			Expression<Func<TValue, bool>> where = null,
			Sort<TValue> sort = default, 
			Pagination limit = default)
		{
			if (where != null)
				query = query.Where(where);
			
			Expression<Func<TValue, object>> sortKey = sort.Key ?? defaultSort;
			Expression sortExpression = sortKey.Body.NodeType == ExpressionType.Convert
				? ((UnaryExpression)sortKey.Body).Operand
				: sortKey.Body;
			
			if (typeof(Enum).IsAssignableFrom(sortExpression.Type))
				throw new ArgumentException("Invalid sort key.");

			query = sort.Descendant ? query.OrderByDescending(sortKey) : query.OrderBy(sortKey);

			if (limit.AfterID != 0)
			{
				TValue after = await get(limit.AfterID);
				Expression key = Expression.Constant(sortKey.Compile()(after), sortExpression.Type);
				query = query.Where(Expression.Lambda<Func<TValue, bool>>(
					ApiHelper.StringCompatibleExpression(Expression.GreaterThan, sortExpression, key),
					sortKey.Parameters.First()
				));
			}
			if (limit.Count > 0)
				query = query.Take(limit.Count);

			return await query.ToListAsync();
		}
		
		public abstract Task<T> Create([NotNull] T obj);

		public virtual async Task<T> CreateIfNotExists(T obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			T old = await Get(obj.Slug);
			if (old != null)
				return old;
			try
			{
				return await Create(obj);
			}
			catch (DuplicatedItemException)
			{
				old = await Get(obj.Slug);
				if (old == null)
					throw new SystemException("Unknown database state.");
				return old;
			}
		}
		
		public virtual async Task<T> Edit(T edited, bool resetOld)
		{
			if (edited == null)
				throw new ArgumentNullException(nameof(edited));
			
			T old = await Get(edited.Slug);

			if (old == null)
				throw new ItemNotFound($"No ressource found with the slug {edited.Slug}.");
			
			if (resetOld)
				Utility.Nullify(old);
			Utility.Merge(old, edited);
			await Validate(old);
			await Database.SaveChangesAsync();
			return old;
		}
		
		protected virtual Task Validate(T ressource)
		{
			foreach (PropertyInfo property in typeof(T).GetProperties()
				.Where(x => typeof(IEnumerable).IsAssignableFrom(x.PropertyType) 
				            && !typeof(string).IsAssignableFrom(x.PropertyType)))
			{
				object value = property.GetValue(ressource);
				if (value is ICollection || value == null)
					continue;
				value = Utility.RunGenericMethod(typeof(Enumerable), "ToList", Utility.GetEnumerableType((IEnumerable)value), value);
				property.SetValue(ressource, value);
			}
			return Task.CompletedTask;
		}
		
		public virtual async Task Delete(int id)
		{
			T ressource = await Get(id);
			await Delete(ressource);
		}

		public virtual async Task Delete(string slug)
		{
			T ressource = await Get(slug);
			await Delete(ressource);
		}

		public abstract Task Delete(T obj);

		public virtual async Task DeleteRange(IEnumerable<T> objs)
		{
			foreach (T obj in objs)
				await Delete(obj);
		}
		
		public virtual async Task DeleteRange(IEnumerable<int> ids)
		{
			foreach (int id in ids)
				await Delete(id);
		}
		
		public virtual async Task DeleteRange(IEnumerable<string> slugs)
		{
			foreach (string slug in slugs)
				await Delete(slug);
		}
	}
	
	public abstract class LocalRepository<T, TInternal> : LocalRepository<TInternal>, IRepository<T>
		where T : class, IResource
		where TInternal : class, T, new()
	{
		protected LocalRepository(DbContext database) : base(database) { }

		public new Task<T> Get(int id)
		{
			return base.Get(id).Cast<T>();
		}
		
		public new Task<T> Get(string slug)
		{
			return base.Get(slug).Cast<T>();
		}

		public abstract Task<ICollection<T>> Search(string query);
		
		public virtual Task<ICollection<T>> GetAll(Expression<Func<T, bool>> where = null,
			Sort<T> sort = default,
			Pagination limit = default)
		{
			return ApplyFilters(Database.Set<TInternal>(), where, sort, limit);
		}
		
		protected virtual async Task<ICollection<T>> ApplyFilters(IQueryable<TInternal> query,
			Expression<Func<T, bool>> where = null,
			Sort<T> sort = default, 
			Pagination limit = default)
		{
			ICollection<TInternal> items = await ApplyFilters(query, 
				base.Get,
				DefaultSort,
				where.Convert<Func<TInternal, bool>>(), 
				sort.To<TInternal>(), 
				limit);

			return items.ToList<T>();
		}
		
		public abstract override Task<TInternal> Create(TInternal obj);

		Task<T> IRepository<T>.Create(T item)
		{
			TInternal obj = new TInternal();
			Utility.Assign(obj, item);
			return Create(obj).Cast<T>()
				.Then(x => item.ID = x.ID);
		}

		Task<T> IRepository<T>.CreateIfNotExists(T item)
		{
			TInternal obj = new TInternal();
			Utility.Assign(obj, item);
			return CreateIfNotExists(obj).Cast<T>()
				.Then(x => item.ID = x.ID);
		}

		public Task<T> Edit(T edited, bool resetOld)
		{
			TInternal obj = new TInternal();
			Utility.Assign(obj, edited);
			return base.Edit(obj, resetOld).Cast<T>();
		}

		public abstract override Task Delete([NotNull] TInternal obj);

		Task IRepository<T>.Delete(T obj)
		{
			TInternal item = new TInternal();
			Utility.Assign(item, obj);
			return Delete(item);
		}
		
		public virtual async Task DeleteRange(IEnumerable<T> objs)
		{
			foreach (T obj in objs)
				await ((IRepository<T>)this).Delete(obj);
		}
	}
}