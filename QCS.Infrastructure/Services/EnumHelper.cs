using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace QCS.Infrastructure.Services
{
    public static class EnumHelper
    {
        public class EnumResultDto
        {
            public int Id { get; set; }
            public string Name { get; set; }        // ชื่อภาษาอังกฤษ (Key)
            public string DisplayName { get; set; } // ชื่อภาษาไทย (Value)
        }

        public static List<EnumResultDto> ToList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                       .Cast<T>()
                       .Select(e => new EnumResultDto
                       {
                           Id = Convert.ToInt32(e),
                           Name = e.ToString(),
                           DisplayName = GetDisplayValue(e)
                       })
                       .ToList();
        }

        private static string GetDisplayValue(object value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute == null ? value.ToString() : attribute.Name;
        }
    }
}