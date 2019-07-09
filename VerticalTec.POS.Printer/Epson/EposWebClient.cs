using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VerticalTec.POS.Printer.Epson;

namespace VerticalTec.Device.Printer.Epson
{
    class EposWebClient
    {
        HttpClient _httpClient;

        public EposWebClient() : this(5)
        {
        }

        public EposWebClient(int timeout)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            _httpClient.DefaultRequestHeaders.Add("SOAPAction", @"""");
            _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        }

        public string EposDeviceName { get; set; }

        public async Task<EpsonResponse> SendRequest(string uri, bool checkStatus, XElement request)
        {
            EpsonResponse epResponse = new EpsonResponse();
            try
            {
                var data = new StringContent(request.ToString(), Encoding.UTF8);
                var responseMessage = await _httpClient.PostAsync(uri, data);
                var content = await responseMessage.Content.ReadAsStringAsync();
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    epResponse = HandlerEpsonResponse(content);
                }
                else
                {
                    epResponse.Success = false;
                    epResponse.Message = $"Can't connect to printer! with status code {responseMessage.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                epResponse.Code = "ConnectionError";
                if(ex is HttpRequestException)
                {
                    epResponse.Success = false;
                    epResponse.Message = $"Can't connect to printer!";
                }
                else if (ex is TaskCanceledException)
                {
                    epResponse.Success = checkStatus == false ? true : false;
                    epResponse.Message = $"Can't connect to printer! Connection time out!";
                }
                else
                {
                    epResponse.Message = $"Can't connect to {EposDeviceName}";
                }
            }
            return epResponse;
        }

        EpsonResponse HandlerEpsonResponse(string result)
        {
            var epResponse = new EpsonResponse();
            var soap = XElement.Parse(result);
            var parameter = (from el in soap.Descendants(PrinterCommand.eposNs + "parameter")
                             select el).FirstOrDefault();
            var response = (from el in soap.Descendants(PrinterCommand.eposNs + "response")
                            select el).FirstOrDefault();
            try
            {
                epResponse.JobId = parameter.Element(PrinterCommand.eposNs + "printjobid").Value;
            }
            catch (Exception)
            {
            }
            if (response.Attribute("success").Value == "true")
            {
                epResponse.Success = true;
            }
            else
            {
                var code = response.Attribute("code").Value;
                epResponse.Code = code;
                epResponse.Message = GetErrorMessageFromCode(code);
            }
            return epResponse;
        }

        string GetErrorMessageFromCode(string code)
        {
            string message = "";
            switch (code)
            {
                case "EPTR_AUTOMATICAL":
                    message = "Automatic recovery error";
                    break;
                case "EPTR_BATTERY_LOW":
                    message = "Battery has run out";
                    break;
                case "EPTR_COVER_OPEN":
                    message = "Cover is open";
                    break;
                case "EPTR_CUTTER":
                    message = "Auto cutter error";
                    break;
                case "EPTR_MECHANICAL":
                    message = "Mechanical error";
                    break;
                case "EPTR_REC_EMPTY":
                    message = "No paper is left in the roll paper";
                    break;
                case "EPTR_UNRECOVERABLE":
                    message = "Unrecoverable error";
                    break;
                case "SchemaError":
                    message = "Error exists in the requested document syntax";
                    break;
                case "DeviceNotFound":
                    message = "Printer specified by the device ID does not exist";
                    break;
                case "PrintSystemError":
                    message = "Error occurred with the printing system";
                    break;
                case "EX_BADPORT":
                    message = "An error occurred with the communication port";
                    break;
                case "EX_TIMEOUT":
                    message = "Print timeout";
                    break;
                case "EX_SPOOLER":
                    message = "Print queue is full";
                    break;
                case "JobNotFound":
                    message = "Specified job ID does not exist";
                    break;
                case "Printing":
                    message = "Printing in progress";
                    break;
                case "JobSpooling":
                    message = "Job is spooling.";
                    break;
                case "TooManyRequests":
                    message = "The number of print jobs sent to the printer has exceeded the allowable limit.";
                    break;
                case "RequestEntityTooLarge":
                    message = "The size of the print job data exceeds the capacity of the printer.";
                    break;
            }
            return message;
        }
    }
}
