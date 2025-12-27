using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TripsProject.Validation
{
    public class DateGreaterThanAttribute : ValidationAttribute
    {
        private readonly string _comparisonProperty;

        public DateGreaterThanAttribute(string comparisonProperty)
        {
            _comparisonProperty = comparisonProperty;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var currentValue = value as DateTime?;
            if (currentValue == null)
                return ValidationResult.Success;

            var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
            if (property == null)
                throw new ArgumentException("Property not found");

            var comparisonValue = property.GetValue(validationContext.ObjectInstance) as DateTime?;

            if (comparisonValue == null)
                return ValidationResult.Success;

            if (currentValue <= comparisonValue)
            {
                return new ValidationResult(ErrorMessage ??
                                            $"{validationContext.DisplayName} must be later than {_comparisonProperty}");
            }

            return ValidationResult.Success;
        }
    }
}