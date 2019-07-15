<%@ Page Language="vb"%>
<%@ Import Namespace="System.Net.Http" %>
<%@ Import Namespace="Newtonsoft.Json" %>
<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        <p runat="server" id="info" />
    </form>
</body>
</html>
<script language="VB" runat="server">
    Sub Page_Load()

        SendData("http://192.168.1.30:9000/v1/sale/sendtohq?shopid=4")
        SendData("http://192.168.1.30:9000/v1/inv/sendtohq?docDate=&shopid=4")
    End Sub

    Private Sub SendData(ByVal url)
        Dim client As HttpClient = New HttpClient()
        Dim respMessage = client.GetAsync(url).Result
        Dim respContent = respMessage.Content.ReadAsStringAsync.Result
        Dim respBody = JsonConvert.DeserializeObject(Of Object)(respContent)
        If respMessage.IsSuccessStatusCode Then
            info.InnerHtml = respBody("message")
        Else
            info.InnerHtml = respBody("message")
        End If
    End Sub
</script>
