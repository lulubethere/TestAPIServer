using System;
using System.Collections.Generic;
using System.Text;

namespace TestWinformClient.Model
{
    internal class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
