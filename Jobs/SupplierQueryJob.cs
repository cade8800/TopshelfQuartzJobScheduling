﻿using EasErpQuerySys.EasApplication;
using EasErpQuerySys.EasApplication.Dto;
using EasErpQuerySys.UpdateApplication;
using log4net;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Configuration;
using System.Linq;
using System.Xml.Linq;

namespace EasErpQuerySys.JobScheduling.Jobs
{
    public sealed class SupplierQueryJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(SupplierQueryJob));
        private readonly IEasAppService _easAppService = new EasAppService();
        private readonly IUpdateAppService _updateAppService = new UpdateAppService();
        private int _requestCount = 0;
        private int _maximumNumOfRequests = string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MaximumNumOfRequests"])
            ? 10 : int.Parse(ConfigurationManager.AppSettings["MaximumNumOfRequests"]);
        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat("供应商任务:" + "开始");
            MailJob();
        }

        private void MailJob()
        {
            _requestCount++;
            try
            {
                var input = GetInputFromXml();
                SupplierQueryOutput queryResult = _easAppService.SupplierQuery(input);
                _logger.InfoFormat("供应商任务:" + "请求数据成功");

                queryResult.SupplierList.ForEach(t =>
                {
                    try
                    {
                        var tt = _updateAppService.UpdateSupplierQuery(JsonConvert.SerializeObject(t));
                        _logger.InfoFormat("供应商任务:" + "上传数据成功，本次任务结束");
                    }
                    catch (Exception e)
                    {
                        throw new EasWebServiceException("update_error", e.Message);
                    }
                });
            }
            catch (EasWebServiceException e)
            {
                var result = e.GetExceptionResult();
                if (result.resultStatus == "fail")
                {
                    _logger.InfoFormat("供应商任务:" + "请求数据失败，" + result.resultMsg);
                    if (_requestCount < _maximumNumOfRequests)
                    {
                        MailJob();
                    }
                    else
                    {
                        _logger.InfoFormat("供应商任务:" + "请求次数达到上限" + _maximumNumOfRequests + "，本次任务结束");
                    }
                }
                else if (result.resultStatus == "update_error")
                {
                    _logger.InfoFormat("供应商任务:" + "上传数据失败，" + result.resultMsg);
                }
                else
                {
                    _logger.InfoFormat("供应商任务:" + "请求数据失败，" + result.resultMsg);
                }
            }
        }

        private SupplierQueryInput GetInputFromXml()
        {
            string filePath = "Input/SupplierQueryInput.xml";
            XDocument inputDoc = XDocument.Load(filePath);
            var output = (from x in inputDoc.Elements()
                          select new SupplierQueryInput
                          {
                              customerNumber = x.Element("customerNumber")?.Value,
                              methodName = x.Element("methodName")?.Value,
                              requestId = x.Element("requestId")?.Value,
                              srcSystem = x.Element("srcSystem")?.Value,
                              status = x.Element("status")?.Value,
                          }).FirstOrDefault();
            inputDoc.Elements().First().Element("requestId").Value = Guid.NewGuid().ToString();
            inputDoc.Save(filePath);
            _logger.InfoFormat("供应商任务:" + "获取查询参数成功");
            return output;
        }
    }
}
