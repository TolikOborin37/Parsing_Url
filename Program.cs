using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace A2_Test_Task
{

    public class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                string startPars = DateTime.Now.ToString("dd.MM в HH:mm");
                Console.WriteLine("Парсер запущен {0}", startPars);

                //проверить время работы парсера за один обход
                Stopwatch watch = new Stopwatch();
                watch.Start();

                //строка json
                string json = "";

                //размер записей за один проход
                int size = 2000;

                //строка подключение к бд
                string connectionString = @"Data Source = (LocalDB)\MSSQLLocalDB; AttachDbFilename = C:\Users\tolik\source\repos\A2_Test_Task\db_WoodDeal.mdf; Integrated Security = True; MultipleActiveResultSets = True;";

                //так как кол-во всех данных, а так же номер страницы так же передается через json, то потребуется еще один POST запрос
                HttpWebRequest requestPage = (HttpWebRequest)WebRequest.Create("https://www.lesegais.ru/open-area/graphql");
                requestPage.UserAgent = "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36";
                requestPage.Method = "POST";
                requestPage.ContentType = "application/json";
                requestPage.Date = DateTime.Now;
                string page = $"{{\"query\":\"query SearchReportWoodDealCount($size: Int!, $number: Int!, $filter: Filter, $orders: [Order!]) {{\\n searchReportWoodDeal(filter: $filter, pageable: {{ number: $number, size: $size}}, orders: $orders) {{\\n total\\n number\\n size\\n overallBuyerVolume\\n overallSellerVolume\\n __typename\\n  }}\\n}}\\n\",\"variables\":{{\"size\":{size},\"number\":0,\"filter\":null}},\"operationName\":\"SearchReportWoodDealCount\"}}";
                byte[] sentPage = Encoding.UTF8.GetBytes(page);
                requestPage.ContentLength = sentPage.Length;
                Stream streamPage = requestPage.GetRequestStream();
                streamPage.Write(sentPage, 0, sentPage.Length);
                streamPage.Close();

                //возврат ответа от интернет ресурса
                HttpWebResponse responcePage = (HttpWebResponse)requestPage.GetResponse();

                //получение json данными о кол-ве данных и номер страницы
                Stream stream = responcePage.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                string jsonPage = reader.ReadToEnd();
                responcePage.Close();
                stream.Close();
                string[] split = jsonPage.Split(' ', ':', '\\', '"', ',');
                //кол-во записей
                int recordCount = int.Parse(split[9]);
                // кол-во страницы
                int countPage = recordCount / size;


                //цикл запроса на сайт для вытягивания данных
                for (int i = 0; i <= countPage; i++)
                {
                    Thread.Sleep(4000);
                    string parse = "";

                    //запрос POST на сайт для получения данных
                    HttpWebRequest requestData = (HttpWebRequest)WebRequest.Create("https://www.lesegais.ru/open-area/graphql");
                    requestData.UserAgent = "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36";
                    requestData.Method = "POST";
                    requestData.ContentType = "application/json";
                    requestData.Date = DateTime.Now;
                    string data = $"{{\"query\":\"query SearchReportWoodDeal($size: Int!, $number: Int!, $filter: Filter, $orders: [Order!]) {{\\n searchReportWoodDeal(filter: $filter, pageable: {{ number: $number, size: $size}}, orders: $orders) {{\\n content {{\\n sellerName\\n sellerInn\\n buyerName\\n buyerInn\\n woodVolumeBuyer\\n woodVolumeSeller\\n dealDate\\n dealNumber\\n __typename\\n    }}\\n __typename\\n  }}\\n}}\\n\",\"variables\":{{\"size\":{size},\"number\":{i},\"filter\":null,\"orders\":null}},\"operationName\":\"SearchReportWoodDeal\"}}";
                    byte[] sentData = Encoding.UTF8.GetBytes(data);
                    requestData.ContentLength = sentData.Length;

                    using (Stream streamData = requestData.GetRequestStream())
                    {

                        streamData.Write(sentData, 0, sentData.Length);
                        streamData.Close();
                    }

                    //возврат ответа от интернет ресурса
                    HttpWebResponse responceData = (HttpWebResponse)requestData.GetResponse();

                    //получение json с данными
                    using (Stream receiveStream = responceData.GetResponseStream())
                    {
                        using (StreamReader readerStream = new StreamReader(receiveStream))
                        {
                            parse = readerStream.ReadToEnd();
                        }
                    }

                    //слияние строк
                    parse = parse.Substring(44);
                    parse = parse.Substring(0, parse.Length - 38);
                    parse = parse + ",";
                    json += parse;

                    Console.Clear();
                    Console.Write("Парсер запущен {0}\nКол-во данных:{1}\nCтарниц: {2} \nНа текущей странице {3}\n", startPars, recordCount, countPage, i);

                }

                //добавление начала и конца к строки для десерилизации json
                string jsonStart = "{\"data\":{\"searchReportWoodDeal\":{\"content\":[";
                string jsonEnd = "],\"__typename\":\"PageReportWoodDeal\"}}}";
                json = json.Insert(0, jsonStart);
                json = json.Insert(json.Length, jsonEnd);

                //сконвертировать json в объект
                Root jsonconvert = JsonConvert.DeserializeObject<Root>(json);

                Console.WriteLine("Запущен процесс добавления данных в бд");

                for (int j = 0; j < jsonconvert.data.searchReportWoodDeal.content.Count; j++)
                {
                    //Console.WriteLine(j);
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        //проверка на наличие в бд dealNumber
                        SqlCommand check_dealNumber = new SqlCommand("SELECT COUNT(*) FROM Table_DealWool WHERE dealNumber = @dealNumber", connection);
                        check_dealNumber.Parameters.AddWithValue("@dealNumber", jsonconvert.data.searchReportWoodDeal.content[j].dealNumber);
                        int exist = (int)check_dealNumber.ExecuteScalar();

                        //валидация данных по ИНН на null, и если не null, то какое кол-во символов
                        int lengthInnSeller = string.IsNullOrEmpty(jsonconvert.data.searchReportWoodDeal.content[j].sellerInn) ? 0 : jsonconvert.data.searchReportWoodDeal.content[j].sellerInn.Length;
                        int lengthInnBuyer = string.IsNullOrEmpty(jsonconvert.data.searchReportWoodDeal.content[j].buyerInn) ? 0 : jsonconvert.data.searchReportWoodDeal.content[j].buyerInn.Length;
                        //проверка даты на null
                        bool dateNull = jsonconvert.data.searchReportWoodDeal.content[j].dealDate != null;
                        //проверка даты на то, что дата меньше текущей, но больше 2000.1.1
                        bool dateValidity = jsonconvert.data.searchReportWoodDeal.content[j].dealDate < DateTime.Now && jsonconvert.data.searchReportWoodDeal.content[j].dealDate > new DateTime(2000, 1, 1);

                        if ((lengthInnBuyer == 10 || lengthInnBuyer == 12) &&
                            (lengthInnSeller == 10 || lengthInnSeller == 12) &&
                            dateNull && dateValidity)
                        {
                            //запись проверяется по dealNumber, и если такая запись есть, то делается UPDATE
                            if (exist > 0)
                            {
                                //Console.WriteLine("найден {0} {1}", jsonconvert.data.searchReportWoodDeal.content[j].dealNumber, jsonconvert.data.searchReportWoodDeal.content[j].sellerName);
                                SqlCommand updCommand = new SqlCommand("UPDATE Table_DealWool SET sellerName = @sellerName, sellerInn = @sellerInn, buyerName = @buyerName, buyerInn = @buyerInn, dealDate = @dealDate, Volume = @Volume WHERE dealNumber = @dealNumber", connection);
                                updCommand.Parameters.AddWithValue("@dealNumber", jsonconvert.data.searchReportWoodDeal.content[j].dealNumber);
                                updCommand.Parameters.AddWithValue("@sellerName", jsonconvert.data.searchReportWoodDeal.content[j].sellerName ?? DBNull.Value.ToString());
                                updCommand.Parameters.AddWithValue("@sellerInn", jsonconvert.data.searchReportWoodDeal.content[j].sellerInn ?? DBNull.Value.ToString());
                                updCommand.Parameters.AddWithValue("@buyerName", jsonconvert.data.searchReportWoodDeal.content[j].buyerName ?? DBNull.Value.ToString());
                                updCommand.Parameters.AddWithValue("@buyerInn", jsonconvert.data.searchReportWoodDeal.content[j].buyerInn ?? DBNull.Value.ToString());
                                updCommand.Parameters.AddWithValue("@dealDate", jsonconvert.data.searchReportWoodDeal.content[j].dealDate);
                                updCommand.Parameters.AddWithValue("@Volume", $"Пр: {jsonconvert.data.searchReportWoodDeal.content[j].woodVolumeSeller}" + " / " + $"Пк: {jsonconvert.data.searchReportWoodDeal.content[j].woodVolumeBuyer}");
                                updCommand.ExecuteNonQuery();
                            }
                            //если записи нет, то делатся INSERT
                            else
                            {
                                SqlCommand insCommand = new SqlCommand($"INSERT INTO Table_DealWool (dealNumber, sellerName, sellerInn, buyerName, buyerInn, dealDate,Volume) VALUES (@dealNumber, @sellerName, @sellerInn, @buyerName, @buyerInn, @dealDate, @Volume)", connection);
                                insCommand.Parameters.AddWithValue("@dealNumber", jsonconvert.data.searchReportWoodDeal.content[j].dealNumber);
                                insCommand.Parameters.AddWithValue("@sellerName", jsonconvert.data.searchReportWoodDeal.content[j].sellerName ?? DBNull.Value.ToString());
                                insCommand.Parameters.AddWithValue("@sellerInn", jsonconvert.data.searchReportWoodDeal.content[j].sellerInn ?? DBNull.Value.ToString());
                                insCommand.Parameters.AddWithValue("@buyerName", jsonconvert.data.searchReportWoodDeal.content[j].buyerName ?? DBNull.Value.ToString());
                                insCommand.Parameters.AddWithValue("@buyerInn", jsonconvert.data.searchReportWoodDeal.content[j].buyerInn ?? DBNull.Value.ToString());
                                insCommand.Parameters.AddWithValue("@dealDate", jsonconvert.data.searchReportWoodDeal.content[j].dealDate);
                                insCommand.Parameters.AddWithValue("@Volume", $"Пр: {jsonconvert.data.searchReportWoodDeal.content[j].woodVolumeSeller}" + " / " + $"Пк: {jsonconvert.data.searchReportWoodDeal.content[j].woodVolumeBuyer}");
                                insCommand.ExecuteNonQuery();
                            }

                        }
                        connection.Close();
                    }
                }
                //пример считывание данных с бд
                //using (SqlConnection connection1 = new SqlConnection(connectionString))
                //{
                //    connection1.Open();
                //    SqlCommand myCommand = new SqlCommand("select * from Table_DealWool", connection1);
                //    SqlDataReader dr = myCommand.ExecuteReader();
                //    while (dr.Read())
                //    {
                //        Console.WriteLine("{0} {1}", dr[0], dr[1]);
                //    }
                //}
                watch.Stop();
                Console.WriteLine($"парс завершен со временем {(watch.ElapsedMilliseconds) / 60000} мин");
                Console.WriteLine($"Следующий парс будет {DateTime.Now.AddMinutes(10).ToString(" dd.MM HH:mm")}");

                //пауза на 10 минут
                Thread.Sleep(600000);
                Console.WriteLine("прошло 10 минут");
            }
        }
    }
}
