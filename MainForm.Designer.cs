namespace FlowTurkViewer
{
    partial class MainForm
    {
        /// <summary>
        ///Gerekli tasarımcı değişkeni.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///Kullanılan tüm kaynakları temizleyin.
        /// </summary>
        ///<param name="disposing">yönetilen kaynaklar dispose edilmeliyse doğru; aksi halde yanlış.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer üretilen kod

        /// <summary>
        /// Tasarımcı desteği için gerekli metot - bu metodun 
        ///içeriğini kod düzenleyici ile değiştirmeyin.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelDrag = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelDrag
            // 
            this.labelDrag.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F);
            this.labelDrag.Location = new System.Drawing.Point(0, 0);
            this.labelDrag.Name = "labelDrag";
            this.labelDrag.Size = new System.Drawing.Size(800, 450);
            this.labelDrag.TabIndex = 0;
            this.labelDrag.Text = "Drag an .tr file to start.";
            this.labelDrag.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.labelDrag);
            this.Name = "MainForm";
            this.Text = "FlowTurk Viewer";
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainForm_DragDrop);
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.MainForm_DragOver);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label labelDrag;
    }
}

