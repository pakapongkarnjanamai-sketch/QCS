using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;

namespace QCS.Api.Controllers
{
    [Route("api/[controller]")]
    public abstract class GenericController<T> : Controller where T : BaseEntity
    {
        protected readonly IRepository<T> _repository;
        protected readonly ILogger<GenericController<T>> _logger;

        protected GenericController(IRepository<T> repository, ILogger<GenericController<T>> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        public virtual object Get(DataSourceLoadOptions loadOptions)
        {
            try
            {
                var data = _repository.GetAll();
                return DataSourceLoader.Load(data, loadOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data for {EntityType}", typeof(T).Name);
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public virtual IActionResult GetById(int id)
        {
            try
            {
                var entity = _repository.GetById(id);
                if (entity == null)
                    return NotFound();

                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting {EntityType} with id {Id}", typeof(T).Name, id);
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost]
        public virtual IActionResult Post(string values)
        {
            try
            {
                var model = _repository.New();
                JsonConvert.PopulateObject(values, model);

                if (!TryValidateModel(model))
                    return BadRequest(ModelState);

                _repository.Add(model);
                _repository.SaveChanges();

                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {EntityType}", typeof(T).Name);
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPut]
        public virtual IActionResult Put(int key, string values)
        {
            try
            {
                var model = _repository.GetById(key);
                if (model == null)
                    return NotFound();

                JsonConvert.PopulateObject(values, model);

                if (!TryValidateModel(model))
                    return BadRequest(ModelState);

                _repository.Update(model);
                _repository.SaveChanges();

                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {EntityType} with id {Id}", typeof(T).Name, key);
                return BadRequest(new { Message = ex.Message });
            }
        }

        // เปลี่ยนจาก public เป็น public virtual
        [HttpDelete]
        public virtual IActionResult Delete(int key)
        {
            try
            {
                var model = _repository.GetById(key);
                if (model == null)
                    return NotFound();

                // Check if entity can be deleted (override in specific controllers if needed)
                if (!CanDelete(model))
                    return BadRequest(new { Message = GetDeleteErrorMessage() });

                _repository.Remove(model);
                _repository.SaveChanges();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {EntityType} with id {Id}", typeof(T).Name, key);
                return BadRequest(new { Message = ex.Message });
            }
        }

        // Virtual methods that can be overridden in specific controllers
        protected virtual bool CanDelete(T entity)
        {
            return true;
        }

        protected virtual string GetDeleteErrorMessage()
        {
            return $"Cannot delete {typeof(T).Name} because it is being used by other records.";
        }
    }
}
