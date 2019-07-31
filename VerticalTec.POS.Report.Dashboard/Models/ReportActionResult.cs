using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace VerticalTec.POS.Report.Dashboard.Models
{
    public class ReportActionResult<TResult> : IActionResult
    {
        TResult _data;
        int? _statusCode;
        bool _success;
        string _message;

        public ReportActionResult()
        {
            _statusCode = StatusCodes.Status200OK;
            _success = true;
            _data = default;
        }

        public TResult Data
        {
            set
            {
                _data = value;
            }
        }

        public int? StatusCode
        {
            set
            {
                _statusCode = value;
            }
        }

        public bool Success
        {
            set
            {
                _success = value;
            }
        }

        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
            }
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var body = new ResponseBody<TResult>()
            {
                Success = _success,
                Data = _data,
                Message = _message
            };
            var result = new ObjectResult(body)
            {
                StatusCode = _statusCode
            };
            await result.ExecuteResultAsync(context);
        }
    }
}
