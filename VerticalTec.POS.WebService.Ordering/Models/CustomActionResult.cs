using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VerticalTec.POS.WebService.Ordering.Models
{
    public class CustomActionResult<TResult> : IActionResult
    {
        TResult _body;
        HttpStatusCode _statusCode;
        ErrorCodes _errorCode;
        string _message;
        string _detail;

        public CustomActionResult() : this(default(TResult))
        {
        }

        public CustomActionResult(TResult body) : this(body, "")
        {
        }

        public CustomActionResult(TResult body = default(TResult), string message = "", string detail = "")
        {
            _statusCode = HttpStatusCode.OK;
            _body = body;
            if (typeof(TResult) == typeof(string))
                _body = (TResult)Convert.ChangeType("", typeof(TResult));
            _message = message;
            _detail = detail;
        }

        public TResult Body
        {
            set
            {
                _body = value;
            }
        }

        public HttpStatusCode StatusCode
        {
            set
            {
                _statusCode = value;
            }
        }

        public ErrorCodes ErrorCode
        {
            set
            {
                _errorCode = value;
            }
        }

        public string Message
        {
            set
            {
                _message = value;
            }
        }

        public string Detail
        {
            set
            {
                _detail = value;
            }
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var result = new ObjectResult(_body);
            if (_statusCode != HttpStatusCode.OK)
            {
                var error = new ErrorDetail()
                {
                    ErrCode = _errorCode,
                    Message = _message,
                    Detail = _detail
                };
                result.StatusCode = (int)_statusCode;
                result.Value = error;
            }
            await result.ExecuteResultAsync(context);
        }
    }
}
