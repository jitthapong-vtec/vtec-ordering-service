using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.WebService.DataSync.Models;

namespace VerticalTec.POS.WebService.DataSync.Models
{
    public class HttpActionResult<TResult> : IHttpActionResult
    {
        TResult _data;
        HttpRequestMessage _request;
        HttpStatusCode _statusCode;
        string _message;

        public HttpActionResult(HttpRequestMessage request)
        {
            _request = request;
            _statusCode = HttpStatusCode.OK;
            _data = default(TResult);
        }

        public TResult Data
        {
            set
            {
                _data = value;
            }
        }

        public HttpStatusCode StatusCode
        {
            set
            {
                _statusCode = value;
            }
        }

        public string Message
        {
            set
            {
                _message = value;
            }
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var body = new ResponseBody<TResult>()
            {
                HttpCode = _statusCode,
                Data = _data,
                Message = _message
            };
            var response = _request.CreateResponse(_statusCode, body);
            return Task.FromResult(response);
        }
    }
}