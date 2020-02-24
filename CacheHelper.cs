using Citrix.Utils.Caching;
using Citrix.Utils.Caching.Enum;
using Citrix.Utils.Caching.Interfaces;
using System;
using Citrix.Doti.Core.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Citrix.Doti.Domain.Data;
using Citrix.Logger.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace Citrix.Doti.Service.Helper
{
    public class CacheHelper
    {
        private ICache cache;
        private readonly ILogger logger;
        public TimeSpan CacheDuration { get; set; }

        private IUtilityService utilityService;
        private static bool cacheInstanceTypeIsRedis;

        public CacheHelper()
        {
            cache = CacheFactory.GetInstance();
            cacheInstanceTypeIsRedis = cache.GetType().FullName.ToLower().Contains("redis");
            utilityService = ServiceLocator.Current.Get<IUtilityService>();
            logger = ServiceLocator.Current.Get<ILogger>();
        }

        public void SetCacheDuration(string settingName, int defaultValue = 0)
        {
            var settingValue = utilityService.GetSettingsValueFromConfig(settingName);
            int minutes = 0;

            if (!string.IsNullOrWhiteSpace(settingValue) && int.TryParse(settingValue, out minutes))
            {
                CacheDuration = new TimeSpan(0, minutes, 0);
            }
            else
            {
                CacheDuration = new TimeSpan(0, defaultValue, 0);
            }
        }

        public bool AddToCache(object data, string key, bool serializeWithType = false)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            try
            {
                logger.Log("Key: " + key + " AddToCache Start Time: " + DateTime.Now, "Citrix.Doti.Service", "CacheHelper.AddToCache");
                stopWatch.Start();
                var cacheResponse = cache.Add(data, key, serializeWithType);
                stopWatch.Stop();
                logger.Log("Key: " + key + " AddToCache Completed Successfully or Unsuccessfully. Check for any errors after Start. Time Elapsed: " 
                    + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper.AddToCache");
                return cacheResponse;
            }
            catch(Exception ex)
            {
                stopWatch.Stop();
                logger.Log("Key: " + key + " Add To Cache FAILED! Time Elapsed: " + stopWatch.Elapsed + " Exception: " + ex.ToString(), "Citrix.Doti.Service", "CacheHelper.AddToCache");
                return false;
            }
        }

        public bool AddToCache(object data, string key, TimeSpan duration, bool serializeWithType = false)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            try
            {
                logger.Log("Key: " + key + " AddToCache Start Time: " + DateTime.Now, "Citrix.Doti.Service", "CacheHelper.AddToCache");
                stopWatch.Start();
                var cacheResponse = cache.Add(data, key, duration, ExpirationType.AbsoluteTime, serializeWithType);
                stopWatch.Stop();
                logger.Log("Key: " + key + " AddToCache Completed Successfully or Unsuccessfully. Check for any errors after Start. Time Elapsed: " 
                    + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper.AddToCache");
                return cacheResponse;
            }
            catch(Exception ex)
            {
                stopWatch.Stop();
                logger.Log("Key: " + key + " Add To Cache FAILED! Time Elapsed: " + stopWatch.Elapsed + " Excepton: " + ex.ToString(), "Citrix.Doti.Service", "CacheHelper.AddToCache");
                return false;
            }
        }

        public bool Remove(string key)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            try
            {
                logger.Log("Key: " + key + " Remove Start Time: " + DateTime.Now, "Citrix.Doti.Service", "CacheHelper.Remove");
                stopWatch.Start();
                var cacheResponse = cache.Remove(key);
                stopWatch.Stop();
                logger.Log("Key: " + key + " Remove Completed Successfully or Unsuccessfully. Check for any errors after Start. Time Elapsed: "
                    + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper.Remove");
                return cacheResponse;
            }
            catch(Exception ex)
            {
                stopWatch.Stop();
                logger.Log("Key: " + key + " Remove From Cache FAILED! Time Elapsed: " + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper.Remove");
                return false;
            }
        }

        //TODO: GetChannelQuoteCache(key){break into smaller objs and store them with individual keys..
        //key=>key0(headers)+key1(quotes)+key2(quotes contd..)+..}

        public object GetCachedData<T>(string key, bool slideExpiration = false, TimeSpan duration = new TimeSpan(), bool deserializeWithType = false)
        {
            var deserializationErrors = new List<string>();
            var stopWatch = new System.Diagnostics.Stopwatch();
            try
            {
                logger.Log("Key: " + key + " GetCache Start Time: " + DateTime.Now, "Citrix.Doti.Service", "CacheHelper.GetCache");
                stopWatch.Start();
                var cachedData = cache.GetCache(key);
                stopWatch.Stop();
                logger.Log("Key: " + key + " GetCache Completed Successfully or Unsuccessfully. Check for any errors after Start. Time Elapsed: "
                    + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper.GetCache");
                object returnValue = null;
                if (cachedData != null)
                {
                    if (cacheInstanceTypeIsRedis)
                    {
                        var serializationSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore,
                            Error = (se, ev) =>
                            {
                                deserializationErrors.Add(ev.ErrorContext.Error.Message);
                                //ev.ErrorContext.Handled = true;
                            }
                        };
                        if (deserializeWithType)
                        {
                            serializationSettings.TypeNameHandling = TypeNameHandling.All;
                            serializationSettings.SerializationBinder = new CustomerModelSerializationBinder();
                        }
                        returnValue = JsonConvert.DeserializeObject<T>(cachedData.ToString(), serializationSettings);
                        if (slideExpiration)
                            cache.Add(returnValue, key, duration, ExpirationType.AbsoluteTime);
                    }
                    else
                    {
                        returnValue = cachedData;
                        if (slideExpiration)
                            cache.ExtendExpiration(key, duration);
                    }
                }
                return returnValue;
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                logger.Log("Key: " + key + " Get From Cache FAILED! Time Elapsed: " + " Exception: " + ex.ToString()
                    +(deserializationErrors.Count > 0 ? " Deserialization Errors: " + deserializationErrors.First() : ""), "Citrix.Doti.Service", "CacheHelper.GetCache");
                return null;
            }
        }

        #region Private Helper Members

        private bool RunTheMethod(Action myMethodName, string key)
        {
            var methodName = myMethodName.Method.Name;
            var stopWatch = new System.Diagnostics.Stopwatch();
            logger.Log("Key: " + key + methodName + " Start Time: " + DateTime.Now, "Citrix.Doti.Service", "CacheHelper." + methodName);
            stopWatch.Start();
            myMethodName();   // note: the return value got discarded
            stopWatch.Stop();
            logger.Log("Key: " + key + methodName+ " Completed Successfully or Unsuccessfully. Check for any errors after Start. Time Elapsed: "
                + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper." + methodName);
            return true;
        }
        
                public Tres result<Tres>(object data,string key,TimeSpan duration, Func<Tres>function, bool serializeWithType = false)
        {
            //Tres val = Functionman(data);
            //return val;
            Tres response = default(Tres);
            var stopWatch = new System.Diagnostics.Stopwatch();
            try
            {
                logger.Log("Key: " + key + " AddToCache Start Time: " + DateTime.Now, "Citrix.Doti.Service", "CacheHelper.AddToCache");
                stopWatch.Start();
                response = function();
                stopWatch.Stop();
                logger.Log("Key: " + key + " AddToCache Completed Successfully or Unsuccessfully. Check for any errors after Start. Time Elapsed: "
                    + stopWatch.Elapsed, "Citrix.Doti.Service", "CacheHelper.AddToCache");
                return response;
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                logger.Log("Key: " + key + " Add To Cache FAILED! Time Elapsed: " + stopWatch.Elapsed + " Excepton: " + ex.ToString(), "Citrix.Doti.Service", "CacheHelper.AddToCache");
                return response;
            }
        }
        public bool AddToCache(object data, string key, bool serializeWithType = false)
        {
            return result<bool>(data, key, null, () => {
                
                var cacheResponse = cache.Add(data, key, serializeWithType);
                return cacheResponse;
            }, serializeWithType);
        }
        public bool AddToCache(object data, string key, TimeSpan duration, bool serializeWithType = false)
        {
            return result<bool>(data,key, duration, ()=> {
                bool res = true;
                var cacheResponse = cache.Add(data, key, duration, ExpirationType.AbsoluteTime, serializeWithType);
                return res;
            }, serializeWithType);
        }

        #endregion

    }

    public class CustomerModelSerializationBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            switch (typeName)
            {
                case "Citrix.Doti.Domain.Data.CustomerModel[]": return typeof(CustomerModel[]);
                case "Citrix.Doti.Domain.Data.CustomerModel": return typeof(CustomerModel);
                case "Citrix.Doti.Domain.Data.Distributor[]": return typeof(Distributor[]);
                case "Citrix.Doti.Domain.Data.Distributor": return typeof(Distributor);
                case "Citrix.Doti.Domain.Data.Reseller[]": return typeof(Reseller[]);
                case "Citrix.Doti.Domain.Data.Reseller": return typeof(Reseller);
                default: return base.BindToType(assemblyName, typeName);
            }
        }
    }
}
