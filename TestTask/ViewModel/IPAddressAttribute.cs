using System.ComponentModel.DataAnnotations;
using System.Net;

namespace TestTask.ViewModel
{
    public class IPAddressAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            IPAddress address;
            var res = IPAddress.TryParse(value.ToString(), out address);
            return res ? ValidationResult.Success : new ValidationResult("Invalid IP address");
        }
    }
}
