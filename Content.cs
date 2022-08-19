using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2_Test_Task
{
    //    {
    //  "data": {
    //    "searchReportWoodDeal": {
    //      "content": [
    //        {
    //          "sellerName": "ГАУ РК \"Симферопольское ЛОХ\"", //2. Номер продавца 
    //          "sellerInn": "9102060830", // 3. ИНН Номер продавца
    //          "buyerName": "Физическое лицо", //4. Наименование покупателя
    //          "buyerInn": "", // Инн покупателя
    //          "woodVolumeBuyer": 0.0, // объем древесины покупателя
    //          "woodVolumeSeller": 7.0, // объем древесины продавца
    //          "dealDate": "2022-08-02", // 6. Дата сделки
    //          "dealNumber": "9306000000000000009102060830", //1. Номер декларации
    //          "__typename": "ReportWoodDeal" // отчет о сделке древесины 
    //        }
    //      ]
    //    }
    //  }
    //}

    //использовался онлайн-конвертор https://json2csharp.com/
    public class Content
    {
        public string sellerName { get; set; }
        public string sellerInn { get; set; }
        public string buyerName { get; set; }
        public string buyerInn { get; set; }
        public double woodVolumeBuyer { get; set; }
        public double woodVolumeSeller { get; set; }
        public DateTime? dealDate { get; set; }
        public string dealNumber { get; set; }
        public string __typename { get; set; }
    }

    public class Data
    {
        public SearchReportWoodDeal searchReportWoodDeal { get; set; }
    }

    public class Root
    {
        public Data data { get; set; }
    }

    public class SearchReportWoodDeal
    {
        public List<Content> content { get; set; }
        public string __typename { get; set; }
    }
}
