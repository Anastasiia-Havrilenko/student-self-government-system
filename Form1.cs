```csharp
public partial class Form1 : Form
{
    private LoginResponse _currentUser;
    private readonly HttpClient _httpClient = new HttpClient();

    private const string ApiUrl = "http://127.0.0.1:8000";

    public Form1()
    {
        InitializeComponent();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        this.Text = "StudGov Desktop System";

        ConfigureDataGridViews();
        HideMainTabs();
    }

    private void ConfigureGridStyle(DataGridView grid)
    {
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        grid.RowTemplate.Height = 40;
    }

    private void ConfigureDataGridViews()
    {
        ConfigureGridStyle(dataGridViewNews);
        ConfigureGridStyle(dataGridViewEvents);
        ConfigureGridStyle(dataGridViewRequests);
        ConfigureGridStyle(dataGridViewActivity);
        ConfigureGridStyle(dataGridViewLogs);
    }

    private void HideMainTabs()
    {
        tabControl1.TabPages.Remove(tabPageNews);
        tabControl1.TabPages.Remove(tabPageEvents);
        tabControl1.TabPages.Remove(tabPageRequests);
        tabControl1.TabPages.Remove(tabPageActivity);
        tabControl1.TabPages.Remove(tabPageLogs);
        tabControl1.TabPages.Remove(tabPageUsers);
        tabControl1.TabPages.Remove(tabPageProfile);
    }

    private async void buttonLogin_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(textBoxUsername.Text) ||
            string.IsNullOrWhiteSpace(textBoxPassword.Text))
        {
            MessageBox.Show("Введіть логін і пароль");
            return;
        }

        var loginData = new LoginRequest
        {
            username = textBoxUsername.Text,
            password = textBoxPassword.Text
        };

        try
        {
            string json = JsonSerializer.Serialize(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ApiUrl}/login", content);

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show("Невірний логін або пароль");
                return;
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<LoginResponse>(responseJson);

            _currentUser = user;

            MessageBox.Show($"Вітаємо, {_currentUser.full_name}");

            OpenMainInterface();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка авторизації: " + ex.Message);
        }
    }

    private void OpenMainInterface()
    {
        this.Text =
            $"StudGov Desktop System | {_currentUser.full_name} ({_currentUser.role_name})";

        tabControl1.TabPages.Remove(tabPageLogin);

        tabControl1.TabPages.Add(tabPageProfile);
        tabControl1.TabPages.Add(tabPageNews);
        tabControl1.TabPages.Add(tabPageEvents);
        tabControl1.TabPages.Add(tabPageRequests);
        tabControl1.TabPages.Add(tabPageActivity);
        tabControl1.TabPages.Add(tabPageLogs);
        tabControl1.TabPages.Add(tabPageUsers);

        if (_currentUser.role_code == "student")
        {
            tabControl1.TabPages.Remove(tabPageNews);
            tabControl1.TabPages.Remove(tabPageEvents);
            tabControl1.TabPages.Remove(tabPageRequests);
            tabControl1.TabPages.Remove(tabPageActivity);
            tabControl1.TabPages.Remove(tabPageLogs);
            tabControl1.TabPages.Remove(tabPageUsers);
        }

        if (_currentUser.role_code == "moderator")
        {
            tabControl1.TabPages.Remove(tabPageActivity);
            tabControl1.TabPages.Remove(tabPageLogs);
            tabControl1.TabPages.Remove(tabPageUsers);
        }

        if (_currentUser.role_code == "member")
        {
            tabControl1.TabPages.Remove(tabPageLogs);
            tabControl1.TabPages.Remove(tabPageUsers);
        }

        LoadActivityComboBoxes();
        LoadRoles();
        LoadProfileInfo();
    }

    private async void LoadActivityComboBoxes()
    {
        try
        {
            string eventsJson = await _httpClient.GetStringAsync($"{ApiUrl}/events");
            var eventsList = JsonSerializer.Deserialize<List<EventItem>>(eventsJson);

            var activeEvents = eventsList.FindAll(e => e.is_active);

            comboBoxActivityEvent.DataSource = activeEvents;
            comboBoxActivityEvent.DisplayMember = "title";
            comboBoxActivityEvent.ValueMember = "id";
            comboBoxActivityEvent.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження списків: " + ex.Message);
        }
    }


    // News

    private async void buttonLoadNews_Click(object sender, EventArgs e)
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/news");
            var news = JsonSerializer.Deserialize<List<NewsItem>>(json);

            dataGridViewNews.DataSource = news;
            dataGridViewNews.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewNews.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridViewNews.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewNews.RowTemplate.Height = 40;

            if (dataGridViewNews.Columns.Contains("id"))
                dataGridViewNews.Columns["id"].HeaderText = "ID";

            if (dataGridViewNews.Columns.Contains("title"))
                dataGridViewNews.Columns["title"].HeaderText = "Заголовок";

            if (dataGridViewNews.Columns.Contains("body"))
                dataGridViewNews.Columns["body"].HeaderText = "Текст новини";

            if (dataGridViewNews.Columns.Contains("created_at"))
            {
                dataGridViewNews.Columns["created_at"].HeaderText = "Дата створення";
                dataGridViewNews.Columns["created_at"].DefaultCellStyle.Format =
                    "dd.MM.yyyy HH:mm";
            }

            if (dataGridViewNews.Columns.Contains("is_published"))
                dataGridViewNews.Columns["is_published"].HeaderText = "Опубліковано";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження новин: " + ex.Message);
        }
    }

    private async void buttonAddNews_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(textBoxTitle.Text) ||
            string.IsNullOrWhiteSpace(textBoxBody.Text))
        {
            MessageBox.Show("Заповніть заголовок і текст новини");
            return;
        }

        var news = new NewsCreate
        {
            title = textBoxTitle.Text,
            body = textBoxBody.Text,
            author_user_id = _currentUser.id
        };

        try
        {
            string json = JsonSerializer.Serialize(news);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ApiUrl}/news", content);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Новину успішно додано");

                textBoxTitle.Clear();
                textBoxBody.Clear();

                buttonLoadNews_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка додавання новини");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    // Events

    private async void buttonLoadEvents_Click(object sender, EventArgs e)
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/events");
            var eventsList = JsonSerializer.Deserialize<List<EventItem>>(json);

            dataGridViewEvents.DataSource = eventsList;
            dataGridViewEvents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewEvents.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridViewEvents.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewEvents.RowTemplate.Height = 40;

            if (dataGridViewEvents.Columns.Contains("id"))
                dataGridViewEvents.Columns["id"].HeaderText = "ID";

            if (dataGridViewEvents.Columns.Contains("title"))
                dataGridViewEvents.Columns["title"].HeaderText = "Назва події";

            if (dataGridViewEvents.Columns.Contains("description"))
                dataGridViewEvents.Columns["description"].HeaderText = "Опис";

            if (dataGridViewEvents.Columns.Contains("starts_at"))
            {
                dataGridViewEvents.Columns["starts_at"].HeaderText = "Дата та час";
                dataGridViewEvents.Columns["starts_at"].DefaultCellStyle.Format =
                    "dd.MM.yyyy HH:mm";
            }

            if (dataGridViewEvents.Columns.Contains("location"))
                dataGridViewEvents.Columns["location"].HeaderText = "Місце";

            if (dataGridViewEvents.Columns.Contains("capacity"))
                dataGridViewEvents.Columns["capacity"].HeaderText = "Місць";

            if (dataGridViewEvents.Columns.Contains("requires_registration"))
                dataGridViewEvents.Columns["requires_registration"].HeaderText = "Реєстрація";

            if (dataGridViewEvents.Columns.Contains("created_at"))
            {
                dataGridViewEvents.Columns["created_at"].HeaderText = "Створено";
                dataGridViewEvents.Columns["created_at"].DefaultCellStyle.Format =
                    "dd.MM.yyyy HH:mm";
            }

            if (dataGridViewEvents.Columns.Contains("is_active"))
                dataGridViewEvents.Columns["is_active"].HeaderText = "Активна";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження подій: " + ex.Message);
        }
    }

    private async void buttonAddEvent_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(textBoxEventTitle.Text))
        {
            MessageBox.Show("Введіть назву події");
            return;
        }

        var newEvent = new EventCreate
        {
            title = textBoxEventTitle.Text,
            description = textBoxEventDescription.Text,
            starts_at = dateTimePickerEventStart.Value.ToString("yyyy-MM-dd HH:mm:ss"),
            location = textBoxEventLocation.Text,
            capacity = (int)numericUpDownCapacity.Value,
            requires_registration = checkBoxRequiresRegistration.Checked,
            created_by_user_id = _currentUser.id
        };

        try
        {
            string json = JsonSerializer.Serialize(newEvent);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ApiUrl}/events", content);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Подію успішно додано");

                textBoxEventTitle.Clear();
                textBoxEventDescription.Clear();
                textBoxEventLocation.Clear();
                numericUpDownCapacity.Value = 0;
                checkBoxRequiresRegistration.Checked = false;

                buttonLoadEvents_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка додавання події");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    // Requests

    private async void buttonLoadRequests_Click(object sender, EventArgs e)
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/requests");
            var requests = JsonSerializer.Deserialize<List<RequestItem>>(json);

            dataGridViewRequests.DataSource = requests;

            dataGridViewRequests.Columns["full_name"].HeaderText = "ПІБ";
            dataGridViewRequests.Columns["group_name"].HeaderText = "Група";
            dataGridViewRequests.Columns["phone_number"].HeaderText = "Телефон";
            dataGridViewRequests.Columns["category"].HeaderText = "Категорія";
            dataGridViewRequests.Columns["text"].HeaderText = "Текст звернення";
            dataGridViewRequests.Columns["status"].HeaderText = "Статус";
            dataGridViewRequests.Columns["answer_text"].HeaderText = "Відповідь";
            dataGridViewRequests.Columns["created_at"].HeaderText = "Дата";
            dataGridViewRequests.Columns["is_anonymous"].HeaderText = "Анонімне";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження звернень: " + ex.Message);
        }
    }

    private async void buttonUpdateRequest_Click(object sender, EventArgs e)
    {
        if (dataGridViewRequests.CurrentRow == null)
        {
            MessageBox.Show("Оберіть звернення в таблиці");
            return;
        }

        if (comboBoxRequestStatus.SelectedItem == null)
        {
            MessageBox.Show("Оберіть статус звернення");
            return;
        }

        int requestId = Convert.ToInt32(dataGridViewRequests.CurrentRow.Cells["id"].Value);

        var updateData = new RequestUpdate
        {
            status = comboBoxRequestStatus.SelectedItem.ToString(),
            answer_text = textBoxRequestAnswer.Text,
            handled_by_user_id = _currentUser.id
        };

        try
        {
            string json = JsonSerializer.Serialize(updateData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"{ApiUrl}/requests/{requestId}"
            );

            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Звернення оновлено");

                textBoxRequestAnswer.Clear();
                buttonLoadRequests_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка оновлення звернення");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    // Activity

    private async void buttonLoadActivity_Click(object sender, EventArgs e)
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/activity");
            var activityList = JsonSerializer.Deserialize<List<ActivityItem>>(json);

            dataGridViewActivity.DataSource = activityList;
            dataGridViewActivity.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewActivity.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridViewActivity.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewActivity.RowTemplate.Height = 40;

            if (dataGridViewActivity.Columns.Contains("id"))
                dataGridViewActivity.Columns["id"].Visible = false;

            if (dataGridViewActivity.Columns.Contains("user_id"))
                dataGridViewActivity.Columns["user_id"].Visible = false;

            if (dataGridViewActivity.Columns.Contains("event_id"))
                dataGridViewActivity.Columns["event_id"].Visible = false;

            if (dataGridViewActivity.Columns.Contains("created_by_user_id"))
                dataGridViewActivity.Columns["created_by_user_id"].Visible = false;

            if (dataGridViewActivity.Columns.Contains("student_name"))
                dataGridViewActivity.Columns["student_name"].HeaderText = "Студент";

            if (dataGridViewActivity.Columns.Contains("event_title"))
                dataGridViewActivity.Columns["event_title"].HeaderText = "Подія";

            if (dataGridViewActivity.Columns.Contains("points"))
                dataGridViewActivity.Columns["points"].HeaderText = "Бали";

            if (dataGridViewActivity.Columns.Contains("reason"))
                dataGridViewActivity.Columns["reason"].HeaderText = "Причина";

            if (dataGridViewActivity.Columns.Contains("created_by_name"))
                dataGridViewActivity.Columns["created_by_name"].HeaderText = "Нарахував";

            if (dataGridViewActivity.Columns.Contains("created_at"))
            {
                dataGridViewActivity.Columns["created_at"].HeaderText = "Дата";
                dataGridViewActivity.Columns["created_at"].DefaultCellStyle.Format =
                    "dd.MM.yyyy HH:mm";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження активності: " + ex.Message);
        }
    }

    private async void buttonAddActivity_Click(object sender, EventArgs e)
    {
        if (comboBoxActivityEvent.SelectedValue == null)
        {
            MessageBox.Show("Оберіть подію");
            return;
        }

        if (comboBoxActivityUser.SelectedValue == null)
        {
            MessageBox.Show("Оберіть студента");
            return;
        }

        int eventId = Convert.ToInt32(comboBoxActivityEvent.SelectedValue);
        int userId = Convert.ToInt32(comboBoxActivityUser.SelectedValue);

        var activity = new ActivityCreate
        {
            user_id = userId,
            event_id = eventId,
            points = (int)numericUpDownPoints.Value,
            reason = textBoxActivityReason.Text,
            created_by_user_id = _currentUser.id
        };

        try
        {
            string json = JsonSerializer.Serialize(activity);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ApiUrl}/activity", content);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Бали успішно нараховано");

                comboBoxActivityUser.SelectedIndex = -1;
                textBoxActivityReason.Clear();
                numericUpDownPoints.Value = 1;

                buttonLoadActivity_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка нарахування балів");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    private async void comboBoxActivityEvent_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboBoxActivityEvent.SelectedValue == null)
            return;

        if (!int.TryParse(comboBoxActivityEvent.SelectedValue.ToString(), out int eventId))
            return;

        try
        {
            string json = await _httpClient.GetStringAsync(
                $"{ApiUrl}/events/{eventId}/registrations"
            );

            var registrations =
                JsonSerializer.Deserialize<List<EventRegistrationItem>>(json);

            comboBoxActivityUser.DataSource = registrations;
            comboBoxActivityUser.DisplayMember = "full_name";
            comboBoxActivityUser.ValueMember = "user_id";

            if (registrations.Count == 0)
            {
                MessageBox.Show("На цю подію ще немає зареєстрованих студентів.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження студентів: " + ex.Message);
        }
    }

    // Logs

    private async void buttonLoadLogs_Click(object sender, EventArgs e)
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/system-logs");
            var logs = JsonSerializer.Deserialize<List<SystemLogItem>>(json);

            dataGridViewLogs.DataSource = logs;

            dataGridViewLogs.Columns["id"].Visible = false;
            dataGridViewLogs.Columns["user_id"].Visible = false;

            dataGridViewLogs.Columns["full_name"].HeaderText = "Користувач";
            dataGridViewLogs.Columns["action"].HeaderText = "Дія";
            dataGridViewLogs.Columns["details"].HeaderText = "Опис";
            dataGridViewLogs.Columns["created_at"].HeaderText = "Дата";
            dataGridViewLogs.Columns["created_at"].DefaultCellStyle.Format =
                "dd.MM.yyyy HH:mm";

            dataGridViewLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewLogs.ReadOnly = true;
            dataGridViewLogs.AllowUserToAddRows = false;
            dataGridViewLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження журналу: " + ex.Message);
        }
    }


    // News editing

    private async void buttonUpdateNews_Click(object sender, EventArgs e)
    {
        if (dataGridViewNews.CurrentRow == null)
        {
            MessageBox.Show("Оберіть новину в таблиці");
            return;
        }

        if (string.IsNullOrWhiteSpace(textBoxTitle.Text) ||
            string.IsNullOrWhiteSpace(textBoxBody.Text))
        {
            MessageBox.Show("Заповніть заголовок і текст новини");
            return;
        }

        int newsId = Convert.ToInt32(dataGridViewNews.CurrentRow.Cells["id"].Value);

        var news = new NewsUpdate
        {
            title = textBoxTitle.Text,
            body = textBoxBody.Text,
            is_published = true,
            updated_by_user_id = _currentUser.id
        };

        try
        {
            string json = JsonSerializer.Serialize(news);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"{ApiUrl}/news/{newsId}"
            );

            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Новину успішно оновлено");

                textBoxTitle.Clear();
                textBoxBody.Clear();

                buttonLoadNews_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка редагування новини");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    private void dataGridViewNews_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        DataGridViewRow row = dataGridViewNews.Rows[e.RowIndex];

        textBoxTitle.Text = row.Cells["title"].Value?.ToString();
        textBoxBody.Text = row.Cells["body"].Value?.ToString();
    }

    private async void buttonDeleteNews_Click(object sender, EventArgs e)
    {
        if (dataGridViewNews.CurrentRow == null)
        {
            MessageBox.Show("Оберіть новину в таблиці");
            return;
        }

        int newsId = Convert.ToInt32(dataGridViewNews.CurrentRow.Cells["id"].Value);

        DialogResult result = MessageBox.Show(
            "Ви дійсно хочете видалити цю новину?",
            "Підтвердження видалення",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (result != DialogResult.Yes)
            return;

        try
        {
            var response = await _httpClient.DeleteAsync(
                $"{ApiUrl}/news/{newsId}?user_id={_currentUser.id}"
            );

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Новину успішно видалено");

                textBoxTitle.Clear();
                textBoxBody.Clear();

                buttonLoadNews_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка видалення новини");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }


    // Event editing

    private async void buttonUpdateEvent_Click(object sender, EventArgs e)
    {
        if (dataGridViewEvents.CurrentRow == null)
        {
            MessageBox.Show("Оберіть подію в таблиці");
            return;
        }

        if (string.IsNullOrWhiteSpace(textBoxEventTitle.Text))
        {
            MessageBox.Show("Введіть назву події");
            return;
        }

        int eventId = Convert.ToInt32(dataGridViewEvents.CurrentRow.Cells["id"].Value);

        var eventData = new EventUpdate
        {
            title = textBoxEventTitle.Text,
            description = textBoxEventDescription.Text,
            starts_at = dateTimePickerEventStart.Value.ToString("yyyy-MM-dd HH:mm:ss"),
            location = textBoxEventLocation.Text,
            capacity = (int)numericUpDownCapacity.Value,
            requires_registration = true,
            is_active = true
        };

        try
        {
            string json = JsonSerializer.Serialize(eventData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"{ApiUrl}/events/{eventId}"
            );

            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Подію успішно оновлено");

                textBoxEventTitle.Clear();
                textBoxEventDescription.Clear();
                textBoxEventLocation.Clear();
                numericUpDownCapacity.Value = 0;

                buttonLoadEvents_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка редагування події");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    private void dataGridViewEvents_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        DataGridViewRow row = dataGridViewEvents.Rows[e.RowIndex];

        textBoxEventTitle.Text = row.Cells["title"].Value?.ToString();
        textBoxEventDescription.Text = row.Cells["description"].Value?.ToString();
        textBoxEventLocation.Text = row.Cells["location"].Value?.ToString();

        if (row.Cells["capacity"].Value != null &&
            int.TryParse(row.Cells["capacity"].Value.ToString(), out int capacity))
        {
            numericUpDownCapacity.Value = capacity;
        }

        if (row.Cells["starts_at"].Value != null &&
            DateTime.TryParse(row.Cells["starts_at"].Value.ToString(), out DateTime startsAt))
        {
            dateTimePickerEventStart.Value = startsAt;
        }
    }

    private async void buttonDeleteEvent_Click(object sender, EventArgs e)
    {
        if (dataGridViewEvents.CurrentRow == null)
        {
            MessageBox.Show("Оберіть подію в таблиці");
            return;
        }

        int eventId = Convert.ToInt32(dataGridViewEvents.CurrentRow.Cells["id"].Value);

        DialogResult result = MessageBox.Show(
            "Ви дійсно хочете видалити цю подію?",
            "Підтвердження видалення",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (result != DialogResult.Yes)
            return;

        try
        {
            var response = await _httpClient.DeleteAsync($"{ApiUrl}/events/{eventId}");

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Подію успішно видалено");

                textBoxEventTitle.Clear();
                textBoxEventDescription.Clear();
                textBoxEventLocation.Clear();
                numericUpDownCapacity.Value = 0;

                buttonLoadEvents_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка видалення події");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }

    // Users

    private async void LoadRoles()
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/roles");
            var roles = JsonSerializer.Deserialize<List<RoleItem>>(json);

            comboBoxUserRole.DataSource = roles;
            comboBoxUserRole.DisplayMember = "name";
            comboBoxUserRole.ValueMember = "id";
            comboBoxUserRole.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження ролей: " + ex.Message);
        }
    }

    private async void buttonLoadUsers_Click(object sender, EventArgs e)
    {
        try
        {
            string json = await _httpClient.GetStringAsync($"{ApiUrl}/users");
            var users = JsonSerializer.Deserialize<List<UserItem>>(json);

            dataGridViewUsers.DataSource = users;
            dataGridViewUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewUsers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridViewUsers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewUsers.RowTemplate.Height = 40;

            if (dataGridViewUsers.Columns.Contains("id"))
                dataGridViewUsers.Columns["id"].HeaderText = "ID";

            if (dataGridViewUsers.Columns.Contains("username"))
                dataGridViewUsers.Columns["username"].HeaderText = "Логін";

            if (dataGridViewUsers.Columns.Contains("full_name"))
                dataGridViewUsers.Columns["full_name"].HeaderText = "ПІБ";

            if (dataGridViewUsers.Columns.Contains("telegram_id"))
                dataGridViewUsers.Columns["telegram_id"].HeaderText = "Telegram ID";

            if (dataGridViewUsers.Columns.Contains("is_active"))
                dataGridViewUsers.Columns["is_active"].HeaderText = "Активний";

            if (dataGridViewUsers.Columns.Contains("created_at"))
            {
                dataGridViewUsers.Columns["created_at"].HeaderText = "Дата створення";
                dataGridViewUsers.Columns["created_at"].DefaultCellStyle.Format =
                    "dd.MM.yyyy HH:mm";
            }

            if (dataGridViewUsers.Columns.Contains("role_code"))
                dataGridViewUsers.Columns["role_code"].Visible = false;

            if (dataGridViewUsers.Columns.Contains("role_name"))
                dataGridViewUsers.Columns["role_name"].HeaderText = "Роль";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка завантаження користувачів: " + ex.Message);
        }
    }

    private void dataGridViewUsers_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        DataGridViewRow row = dataGridViewUsers.Rows[e.RowIndex];

        if (row.Cells["is_active"].Value != null)
            checkBoxUserActive.Checked = Convert.ToBoolean(row.Cells["is_active"].Value);

        if (row.Cells["role_name"].Value != null)
        {
            string roleName = row.Cells["role_name"].Value.ToString();

            foreach (RoleItem role in comboBoxUserRole.Items)
            {
                if (role.name == roleName)
                {
                    comboBoxUserRole.SelectedItem = role;
                    break;
                }
            }
        }
    }

    private async void buttonUpdateUser_Click(object sender, EventArgs e)
    {
        if (dataGridViewUsers.CurrentRow == null)
        {
            MessageBox.Show("Оберіть користувача в таблиці");
            return;
        }

        if (comboBoxUserRole.SelectedValue == null)
        {
            MessageBox.Show("Оберіть роль користувача");
            return;
        }

        int userId = Convert.ToInt32(dataGridViewUsers.CurrentRow.Cells["id"].Value);

        var updateData = new UserUpdate
        {
            role_id = Convert.ToInt32(comboBoxUserRole.SelectedValue),
            is_active = checkBoxUserActive.Checked,
            updated_by_user_id = _currentUser.id
        };

        try
        {
            string json = JsonSerializer.Serialize(updateData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"{ApiUrl}/users/{userId}"
            );

            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Обліковий запис успішно оновлено");
                buttonLoadUsers_Click(null, null);
            }
            else
            {
                MessageBox.Show("Помилка оновлення облікового запису");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка: " + ex.Message);
        }
    }


    // Profile

    private void LoadProfileInfo()
    {
        labelProfileUsername.Text = _currentUser.username;
        labelProfileRole.Text = _currentUser.role_name;
    }

    private void buttonChangeUser_Click(object sender, EventArgs e)
    {
        DialogResult result = MessageBox.Show(
            "Вийти з поточного облікового запису?",
            "Підтвердження",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (result != DialogResult.Yes)
            return;

        _currentUser = null;

        tabControl1.TabPages.Clear();
        tabControl1.TabPages.Add(tabPageLogin);

        textBoxUsername.Clear();
        textBoxPassword.Clear();

        this.Text = "StudGov Desktop System";

        textBoxUsername.Focus();
    }
}

// Data models

    public class NewsItem
    {
        public int id { get; set; }
        public string title { get; set; }
        public string body { get; set; }
        public DateTime created_at { get; set; }
        public bool is_published { get; set; }
    }

    
    public class NewsCreate
    {
        public string title { get; set; }
        public string body { get; set; }
        public int author_user_id { get; set; }
    }

    public class NewsUpdate
    {
        public string title { get; set; }
        public string body { get; set; }
        public bool is_published { get; set; }
        public int? updated_by_user_id { get; set; }
    }

    public class EventUpdate
    {
        public string title { get; set; }
        public string description { get; set; }
        public string starts_at { get; set; }
        public string location { get; set; }
        public int capacity { get; set; }
        public bool requires_registration { get; set; }
        public bool is_active { get; set; }
    } 

    
    public class EventItem
    {
        public int id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public DateTime starts_at { get; set; }
        public string location { get; set; }
        public int? capacity { get; set; }
        public bool requires_registration { get; set; }
        public DateTime created_at { get; set; }
        public bool is_active { get; set; }
    }

    
    public class EventCreate
    {
        public string title { get; set; }
        public string description { get; set; }
        public string starts_at { get; set; }
        public string location { get; set; }
        public int capacity { get; set; }
        public bool requires_registration { get; set; }
        public int created_by_user_id { get; set; }
    }

    public class EventRegistrationItem
    {
        public int registration_id { get; set; }
        public int event_id { get; set; }
        public int user_id { get; set; }
        public string full_name { get; set; }
        public string group_name { get; set; }
        public string phone_number { get; set; }
        public DateTime registered_at { get; set; }
        public string status { get; set; }

        public override string ToString()
        {
            return $"{full_name} ({group_name})";
        }
    }


    
    public class RequestItem
    {
        public int id { get; set; }
        public int? user_id { get; set; }
        public string full_name { get; set; }
        public string group_name { get; set; }
        public string phone_number { get; set; }
        public bool is_anonymous { get; set; }
        public string category { get; set; }
        public string text { get; set; }
        public string status { get; set; }
        public string answer_text { get; set; }
        public int? handled_by_user_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }

    
    public class RequestUpdate
    {
        public string status { get; set; }
        public string answer_text { get; set; }
        public int? handled_by_user_id { get; set; }
    }

    
    public class ActivityItem
    {
        public int id { get; set; }
        public int user_id { get; set; }
        public string student_name { get; set; }
        public int? event_id { get; set; }
        public string event_title { get; set; }
        public int points { get; set; }
        public string reason { get; set; }
        public int? created_by_user_id { get; set; }
        public string created_by_name { get; set; }
        public DateTime created_at { get; set; }
    }

    
    public class ActivityCreate
    {
        public int user_id { get; set; }
        public int? event_id { get; set; }
        public int points { get; set; }
        public string reason { get; set; }
        public int? created_by_user_id { get; set; }
    }


    public class SystemLogItem
    {
        public int id { get; set; }
        public int? user_id { get; set; }
        public string full_name { get; set; }
        public string action { get; set; }
        public string details { get; set; }
        public DateTime created_at { get; set; }
    }

    public class LoginRequest
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    public class LoginResponse
    {
        public int id { get; set; }
        public string username { get; set; }
        public string full_name { get; set; }
        public string role_code { get; set; }
        public string role_name { get; set; }
    }

    public class UserItem
    {
        public int id { get; set; }
        public string username { get; set; }
        public string full_name { get; set; }
        public long? telegram_id { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public string role_code { get; set; }
        public string role_name { get; set; }
    }

    public class RoleItem
    {
        public int id { get; set; }
        public string code { get; set; }
        public string name { get; set; }
    }

    public class UserUpdate
    {
        public int? role_id { get; set; }
        public bool? is_active { get; set; }
        public int? updated_by_user_id { get; set; }
    }
}

```
