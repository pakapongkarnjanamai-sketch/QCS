using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;

namespace QCS.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController] // เพิ่ม Attribute นี้ช่วยเรื่อง Model Validation อัตโนมัติ
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
            // DataSourceLoader ทำงานกับ IQueryable ได้ดีที่สุด (filter ที่ DB)
            return DataSourceLoader.Load(_repository.GetAll(), loadOptions);
        }

        [HttpGet("{id}")]
        public virtual async Task<IActionResult> GetById(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                return NotFound();

            return Ok(entity);
        }

        [HttpPost]
        public virtual async Task<IActionResult> Post([FromForm] string values)
        {
            // Note: DevExtreme DataGrid มักส่ง values มาเป็น key-value string (form-data)
            try
            {
                var model = _repository.New();
                JsonConvert.PopulateObject(values, model);

                if (!TryValidateModel(model))
                    return BadRequest(ModelState);

                await _repository.AddAsync(model);
                await _repository.SaveChangesAsync();

                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {EntityType}", typeof(T).Name);
                return BadRequest(new { Message = "An error occurred while creating the record." });
            }
        }

        [HttpPut]
        public virtual async Task<IActionResult> Put(int key, [FromForm] string values)
        {
            try
            {
                var model = await _repository.GetByIdAsync(key);
                if (model == null)
                    return NotFound();

                JsonConvert.PopulateObject(values, model);

                if (!TryValidateModel(model))
                    return BadRequest(ModelState);

                await _repository.UpdateAsync(model);
                await _repository.SaveChangesAsync();

                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {EntityType} with id {Id}", typeof(T).Name, key);
                return BadRequest(new { Message = "An error occurred while updating the record." });
            }
        }

        [HttpDelete]
        public virtual async Task<IActionResult> Delete(int key)
        {
            try
            {
                var model = await _repository.GetByIdAsync(key);
                if (model == null)
                    return NotFound();

                if (!CanDelete(model))
                    return BadRequest(new { Message = GetDeleteErrorMessage() });

                await _repository.RemoveAsync(model);
                await _repository.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {EntityType} with id {Id}", typeof(T).Name, key);
                return BadRequest(new { Message = "An error occurred while deleting the record." });
            }
        }

        protected virtual bool CanDelete(T entity) => true;

        protected virtual string GetDeleteErrorMessage() =>
            $"Cannot delete {typeof(T).Name} because it is being used by other records.";
    }
}