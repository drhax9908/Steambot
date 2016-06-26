using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ExempleBot
{
    public class Database
    {
        public MySqlConnection connection { get; set; }
        public bool isConnected
        {
            get
            {
                if (connection != null)
                    return connection.State == ConnectionState.Open;
                return false;
            }
        }

        public Database(string server, string username, string password, string port, string database)
        {
            string connetionString = null;
            if (database.Length > 0)
                connetionString = "Server=" + server + ";Port=" + port + ";Database=" + database + ";Uid=" + username + ";Pwd=" + password;
            else
                connetionString = "Server=" + server + "Port=" + port + ";Uid=" + username + ";Pwd=" + password;

            try
            {
                if (database.Length > 0)
                    Console.WriteLine("Connecting to database...");
                else
                    Console.WriteLine("Connecting to database server... [TRYING TO CREATE DATABASE]");
                connection = new MySqlConnection(connetionString);
                connection.Open();
                Console.WriteLine("Connection successfully done !");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection failed ! (" + ex + ")");
            }
        }

        public List<Dictionary<string, string>> SELECT(string[] rows, string table, string args = "")
        {
            if (!isConnected)
            {
                Console.WriteLine("ERROR: can't do SELECT, not connected to database !");
                return null;
            }

            MySqlCommand command;
            MySqlDataReader dataReader;
            int nbrItems = rows.Length;
            string request = "SELECT ";

            for (int i = 0; i < nbrItems; i++)
            {
                request += rows[i];
                if (i != nbrItems - 1)
                    request += ", ";
            }

            request += " FROM " + table;

            if (args != "")
            {
                request += " " + args;
            }

            request += ";";

            List<Dictionary<string, string>> data = new List<Dictionary<string, string>>();

            command = new MySqlCommand(request, connection);
            dataReader = command.ExecuteReader();
            while (dataReader.Read())
            {
                Dictionary<string, string> track = new Dictionary<string, string>();
                for (int i = 0; i < nbrItems; i++)
                    track.Add(rows[i], dataReader.GetValue(i).ToString());
                data.Add(track);
            }
            dataReader.Close();
            command.Dispose();

            return data;
        }

        public bool INSERT(string table, string[] rows, string[] values)
        {

            if (!isConnected)
            {
                Console.WriteLine("ERROR: can't do INSERT, not connected to database !");
                return false;
            }


            if (rows.Length != values.Length)
            {
                if (rows.Length > values.Length)
                {
                    Console.WriteLine("There is more cols then values to add !");
                    return false;
                }

                if (rows.Length < values.Length)
                {
                    Console.WriteLine("There is more values then cols to add !");
                    return false;
                }
            }

            MySqlCommand command;
            string request = "INSERT INTO " + table + " (";

            for (int i = 0; i < rows.Length; i++)
            {
                request += "`" + rows[i] + "`";
                if (i != rows.Length - 1)
                    request += ", ";
            }

            request += ") VALUES (";

            for (int i = 0; i < values.Length; i++)
            {
                request += "\"" + values[i] + "\"";
                if (i != values.Length - 1)
                    request += ", ";
            }
            request += ");";

            command = new MySqlCommand(request, connection);
            command.ExecuteNonQuery();
            command.Dispose();

            return true;
        }

        public bool DELETE(string rows, string values, string table)
        {
            if (!isConnected)
            {
                Console.WriteLine("ERROR: can't do DELETE, not connected to database !");
                return false;
            }

            MySqlCommand command;
            int nbrItems = rows.Length;
            string request = "DELETE FROM " + table + " WHERE `" + rows + "`='" + values + "'";

            command = new MySqlCommand(request, connection);
            command.ExecuteNonQuery();
            command.Dispose();

            return true;
        }

        public bool QUERY(string query)
        {
            if (!isConnected)
            {
                Console.WriteLine("ERROR: can't do QUERY, not connected to database !");
                return false;
            }
            MySqlCommand command;
            command = new MySqlCommand(query, connection);
            command.ExecuteNonQuery();
            command.Dispose();

            return true;
        }

    }
}
