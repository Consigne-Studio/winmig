Imports System.IO.Ports

Public Class FormBanc
    Private WithEvents port As New SerialPort("COM1")

    Private Sub Demarrer_Click(sender As Object, e As EventArgs) Handles Demarrer.Click
        If port.IsOpen Then
            port.Close()
        ElseIf Me.InvokeRequired Then
            Me.Invoke(Sub() port.Open())
        End If
    End Sub

    Private Sub Arreter(sender As Object, e As EventArgs)
        Dim n = 0
        For i = 1 To 3
            n += i
        Next
    End Sub
End Class
