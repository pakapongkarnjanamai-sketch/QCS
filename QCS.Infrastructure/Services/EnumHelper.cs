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
            public string Name { get; set; } = string.Empty;        // ชื่อภาษาอังกฤษ (Key)
            public string DisplayName { get; set; } = string.Empty; // ชื่อภาษาไทย (Value)
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

        private static string GetDisplayValue<T>(T value) where T : Enum
        {
            var name = value.ToString();
            var field = typeof(T).GetField(name);
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.GetName() ?? name;
        }
    }
}