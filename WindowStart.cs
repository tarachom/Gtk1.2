using Gtk;
using Npgsql;

namespace GtkTest
{
    class WindowStart : Window
    {
        NpgsqlDataSource? DataSource { get; set; }

        ListStore? Store;
        TreeView? treeView;

        public WindowStart() : base("PostgreSQL + GTKSharp")
        {
            SetDefaultSize(1600, 900);
            SetPosition(WindowPosition.Center);

            DeleteEvent += delegate { Program.Quit(); };

            VBox vbox = new VBox();
            Add(vbox);

            #region Кнопки

            //Кнопки
            HBox hBoxButton = new HBox();
            vbox.PackStart(hBoxButton, false, false, 10);

            Button bConnect = new Button("Підключитись до PostgreSQL");
            bConnect.Clicked += OnConnect;
            hBoxButton.PackStart(bConnect, false, false, 10);

            Button bFill = new Button("Заповнити");
            bFill.Clicked += OnFill;
            hBoxButton.PackStart(bFill, false, false, 10);

            Button bAdd = new Button("Додати файл");
            bAdd.Clicked += OnAdd;
            hBoxButton.PackStart(bAdd, false, false, 10);

            Button bSave = new Button("Вигрузити файл");
            bSave.Clicked += OnSave;
            hBoxButton.PackStart(bSave, false, false, 10);

            Button bDelete = new Button("Видалити файл");
            bDelete.Clicked += OnDelete;
            hBoxButton.PackStart(bDelete, false, false, 10);

            #endregion

            //Список
            HBox hboxTree = new HBox();
            vbox.PackStart(hboxTree, true, true, 0);

            AddColumn();

            ScrolledWindow scroll = new ScrolledWindow() { ShadowType = ShadowType.In };
            scroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scroll.Add(treeView);

            hboxTree.PackStart(scroll, true, true, 10);


            ShowAll();
        }

        enum Columns
        {
            image,
            id,
            name,
            size
        }

        void AddColumn()
        {
            Store = new ListStore
            (
                typeof(Gdk.Pixbuf),
                typeof(int),       //id
                typeof(string),    //name
                typeof(int)        //size
            );

            treeView = new TreeView(Store);
            treeView.Selection.Mode = SelectionMode.Multiple;

            treeView.AppendColumn(new TreeViewColumn("", new CellRendererPixbuf(), "pixbuf", (int)Columns.image));
            treeView.AppendColumn(new TreeViewColumn("id", new CellRendererText(), "text", (int)Columns.id) { MinWidth = 100 });
            treeView.AppendColumn(new TreeViewColumn("name", new CellRendererText(), "text", (int)Columns.name) { MinWidth = 500 });
            treeView.AppendColumn(new TreeViewColumn("size", new CellRendererText(), "text", (int)Columns.size) { MinWidth = 100 });
        }

        void OnConnect(object? sender, EventArgs args)
        {
            string Server = "localhost";
            string UserId = "postgres";
            string Password = "1";
            int Port = 5432;
            string Database = "test";

            string conString = $"Server={Server};Username={UserId};Password={Password};Port={Port};Database={Database};SSLMode=Prefer;";

            DataSource = NpgsqlDataSource.Create(conString);

            OnFill(this, new EventArgs());
        }

        void OnFill(object? sender, EventArgs args)
        {
            if (DataSource != null)
            {
                Store!.Clear();

                NpgsqlCommand command = DataSource.CreateCommand(
                    "SELECT id, name, size FROM tab2 ORDER BY id");

                NpgsqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    int id = (int)reader["id"];
                    string name = reader["name"].ToString() ?? "";
                    int size = (int)reader["size"];

                    Store!.AppendValues(new Gdk.Pixbuf("doc.png"), id, name, size);
                }

                reader.Close();
            }
        }

        void OnAdd(object? sender, EventArgs args)
        {
            if (DataSource != null)
            {
                string filename = "";
                bool fileSelect = false;

                FileChooserDialog fc = new FileChooserDialog("Виберіть файл для загрузки", this,
                    FileChooserAction.Open, "Закрити", ResponseType.Cancel, "Вибрати", ResponseType.Accept);

                fc.Filter = new FileFilter();
                fc.Filter.AddPattern("*.*");

                if (fc.Run() == (int)ResponseType.Accept)
                {
                    if (!String.IsNullOrEmpty(fc.Filename))
                    {
                        filename = fc.Filename;
                        fileSelect = true;
                    }
                }

                fc.Destroy();

                if (fileSelect)
                {
                    FileInfo fileinfo = new FileInfo(filename);
                    byte[] data = File.ReadAllBytes(filename);

                    NpgsqlCommand command = DataSource.CreateCommand(
                    "INSERT INTO tab2 (name, size, data) VALUES (@name, @size, @data)");

                    command.Parameters.AddWithValue("name", fileinfo.Name);
                    command.Parameters.AddWithValue("size", fileinfo.Length); //byte
                    command.Parameters.AddWithValue("data", data);

                    command.ExecuteNonQuery();
                }

                OnFill(this, new EventArgs());
            }
        }

        void OnSave(object? sender, EventArgs args)
        {
            if (DataSource != null)
            {
                if (treeView!.Selection.CountSelectedRows() != 0)
                {
                    string currentFolder = "";
                    bool isSelect = false;

                    FileChooserDialog fc = new FileChooserDialog("Виберіть каталог для вигрузки файлу", this,
                        FileChooserAction.SelectFolder, "Закрити", ResponseType.Cancel, "Вибрати", ResponseType.Accept);

                    if (fc.Run() == (int)ResponseType.Accept)
                    {
                        if (!String.IsNullOrEmpty(fc.CurrentFolder))
                        {
                            currentFolder = fc.CurrentFolder;
                            isSelect = true;
                        }
                    }

                    fc.Destroy();

                    if (isSelect)
                    {
                        TreePath[] selectionRows = treeView.Selection.GetSelectedRows();

                        foreach (TreePath itemPath in selectionRows)
                        {
                            TreeIter iter;
                            treeView.Model.GetIter(out iter, itemPath);

                            int id = (int)treeView.Model.GetValue(iter, (int)Columns.id);
                            string filename = (string)treeView.Model.GetValue(iter, (int)Columns.name);

                            string fullPath = System.IO.Path.Combine(currentFolder, filename);

                            NpgsqlCommand command = DataSource.CreateCommand(
                                "SELECT data FROM tab2 WHERE id = @id");

                            command.Parameters.AddWithValue("id", id);

                            object? data = command.ExecuteScalar();

                            if (data != null)
                                File.WriteAllBytes(fullPath, (byte[])data);
                        }
                    }
                }
            }
        }

        void OnDelete(object? sender, EventArgs args)
        {
            if (DataSource != null)
            {
                if (treeView!.Selection.CountSelectedRows() != 0)
                {
                    NpgsqlCommand command = DataSource.CreateCommand(
                        "DELETE FROM tab2 WHERE id = @id");

                    TreePath[] selectionRows = treeView.Selection.GetSelectedRows();

                    foreach (TreePath itemPath in selectionRows)
                    {
                        TreeIter iter;
                        treeView.Model.GetIter(out iter, itemPath);

                        int id = (int)treeView.Model.GetValue(iter, (int)Columns.id);

                        command.Parameters.Clear();
                        command.Parameters.Add(new NpgsqlParameter("id", id));

                        command.ExecuteNonQuery();
                    }

                    OnFill(this, new EventArgs());
                }
            }
        }

    }
}
