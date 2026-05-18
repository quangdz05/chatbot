using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Chat_API.DTOs
{
    public class DocumentUploadRequest
    {
        [Required]
        public IFormFile? File { get; set; }
    }
}
