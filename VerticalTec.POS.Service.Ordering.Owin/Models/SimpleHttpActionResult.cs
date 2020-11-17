using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class SimpleHttpActionResult : IHttpActionResult
    {
        HttpRequestMessage _request;
        HttpStatusCode _statusCode;
        string _message;

        public SimpleHttpActionResult(HttpRequestMessage request)
        {
            _request = request;
            _statusCode = HttpStatusCode.OK;
        }

        public string Message {
            get => _message;
            set => _message = value;
        }

        public HttpStatusCode StatusCode
        {
            set
            {
                _statusCode = value;
            }
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = _request.CreateResponse(_statusCode, _message);
            return Task.FromResult(response);
        }
    }
}