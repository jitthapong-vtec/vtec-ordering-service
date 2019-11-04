using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class HttpActionResult<TResult> : IHttpActionResult
    {
        TResult _body;
        HttpRequestMessage _request;
        HttpStatusCode _statusCode;
        ErrorCodes _errorCode;
        bool _success;
        string _message;

        public HttpActionResult(HttpRequestMessage request)
        {
            _request = request;
            _statusCode = HttpStatusCode.OK;
            _body = default(TResult);
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

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = _request.CreateResponse(_statusCode, _body);
            if (_statusCode != HttpStatusCode.OK)
            {
                var error = new ErrorDetail()
                {
                    ErrCode = _errorCode,
                    Message = _message
                };
                response = _request.CreateResponse(_statusCode, error);
            }
            return Task.FromResult(response);
        }
    }
}