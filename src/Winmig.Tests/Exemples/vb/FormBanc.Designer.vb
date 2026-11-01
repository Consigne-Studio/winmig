<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FormBanc
    Inherits System.Windows.Forms.Form

    Private Sub InitializeComponent()
        Me.Demarrer = New System.Windows.Forms.Button()
        Me.Stop1 = New System.Windows.Forms.Button()
        Me.Journal = New System.Windows.Forms.ListBox()
        Me.SuspendLayout()
        '
        'Demarrer
        '
        Me.Demarrer.Location = New System.Drawing.Point(12, 12)
        Me.Demarrer.Size = New System.Drawing.Size(90, 28)
        Me.Demarrer.Text = "Démarrer"
        '
        'Stop1
        '
        Me.Stop1.Location = New System.Drawing.Point(110, 12)
        Me.Stop1.Size = New System.Drawing.Size(90, 28)
        Me.Stop1.Text = "Arrêter"
        AddHandler Me.Stop1.Click, AddressOf Me.Arreter
        '
        'Journal
        '
        Me.Journal.Location = New System.Drawing.Point(12, 50)
        Me.Journal.Size = New System.Drawing.Size(360, 200)
        '
        'FormBanc
        '
        Me.ClientSize = New System.Drawing.Size(384, 262)
        Me.Controls.Add(Me.Journal)
        Me.Controls.Add(Me.Stop1)
        Me.Controls.Add(Me.Demarrer)
        Me.Text = "Banc d'essai"
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents Demarrer As System.Windows.Forms.Button
    Friend WithEvents Stop1 As System.Windows.Forms.Button
    Friend WithEvents Journal As System.Windows.Forms.ListBox
End Class
