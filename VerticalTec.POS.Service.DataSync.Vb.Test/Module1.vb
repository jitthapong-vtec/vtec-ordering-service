Imports System.Net.Http
Imports Newtonsoft.Json

Module Module1

    Sub Main()
        SendData("http://192.168.1.30:9000/v1/sale/sendtohq?shopid=4")
        SendData("http://192.168.1.30:9000/v1/inv/sendtohq?docDate=&shopid=4")
        Console.ReadLine()
    End Sub

    Private Sub SendData(ByVal url)
        Dim client As HttpClient = New HttpClient()
        Console.WriteLine($"Send request {url}")
        Dim respMessage = client.GetAsync(url).Result
        Dim respContent = respMessage.Content.ReadAsStringAsync.Result
        Dim respBody = JsonConvert.DeserializeObject(Of Object)(respContent)
        If respMessage.IsSuccessStatusCode Then
            Console.WriteLine(respBody("message"))
        Else
            Console.WriteLine(respBody("message"))
        End If
    End Sub
End Module
