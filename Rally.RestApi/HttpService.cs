﻿using System;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

namespace Rally.RestApi
{
    internal class HttpService
    {
        readonly CredentialCache credentials;
        readonly CookieContainer cookies = new CookieContainer();
        readonly ConnectionInfo connectionInfo;

        internal Uri Server { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionInfo">Connection Information</param>
        public HttpService(ConnectionInfo connectionInfo)
        {
            this.connectionInfo = connectionInfo;

            if (connectionInfo.authCookie != null)
            {
                setCookieAuth(connectionInfo.authCookie);
            }
            else if (connectionInfo.authType == AuthorizationType.Basic)
            {
                Server = connectionInfo.server;
                credentials = new CredentialCache { { connectionInfo.server, "Basic", new NetworkCredential(connectionInfo.username, connectionInfo.password) } };
            }
            else
            {
                doSSOAuth();
            }
        }

        private void doSSOAuth()
        {
            var ssoHelper = new SSOHelper();
            var success = false;
            
            if (connectionInfo.authType == AuthorizationType.SSOWithCred)
                success = ssoHelper.doHandshake(connectionInfo.server, connectionInfo.username, connectionInfo.password);
            else
                success = ssoHelper.doHandshake(connectionInfo.server);

            if (!success)
                throw new Exception("SSO handshake not successful!");

            setCookieAuth(connectionInfo.authCookie = ssoHelper.jsessionidCookie);
        }

        void setCookieAuth(Cookie cookie)
        {
            Server = new UriBuilder("https",cookie.Domain).Uri;
            cookies.Add(cookie);
        }

        WebClient GetWebClient(IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            var webClient = new CookieAwareWebClient(cookies);
            webClient.Encoding = Encoding.UTF8;
            if (headers != null)
                foreach (var pairs in headers)
                    webClient.Headers.Add(pairs.Key, pairs.Value);
            if (credentials != null)
                webClient.Credentials = credentials;
            if (connectionInfo.proxy != null)
                webClient.Proxy = connectionInfo.proxy;
            return webClient;
        }

        public string Post(Uri target, string data, IDictionary<string, string> headers = null)
        {
            String response = "<No response>";
            DateTime startTime = DateTime.Now;
            String requestHeaders = "";
            String responseHeaders = "";
            String cookiesBefore = "";
            String cookiesAfter = "";

            do
            {
                try
                {
                    using (var webClient = GetWebClient(headers))
                    {
                        cookiesBefore = makeDisplayableCookieString(cookies);
                        requestHeaders = webClient.Headers.ToString();
                        response = webClient.UploadString(target, data);
                        cookiesAfter = makeDisplayableCookieString(cookies);
                        responseHeaders = webClient.ResponseHeaders.ToString();
                        return response;
                    }
                }
                catch (WebException e)
                {
                    if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.Unauthorized &&
                        connectionInfo.authType != AuthorizationType.Basic)
                    {
                        doSSOAuth();
                        continue;
                    }
                    throw;
                }
                finally
                {
                    Trace.TraceInformation("Post ({0}):\r\n{1}\r\nRequest Headers:\r\n{2}Cookies Before:\r\n{3}Request Data:\r\n{4}\r\nResponse Headers:\r\n{5}Cookies After:\r\n{6}Response Data\r\n{7}",
                                           DateTime.Now.Subtract(startTime).ToString(),
                                           target.ToString(),
                                           requestHeaders,
                                           cookiesBefore,
                                           data,
                                           responseHeaders,
                                           cookiesAfter,
                                           response);
                }
            } while (true);
        }

        public string Get(Uri target, IDictionary<string, string> headers = null)
        {
            String response = "<No response>";
            DateTime startTime = DateTime.Now;
            String requestHeaders = "";
            String responseHeaders = "";
            String cookiesBefore = "";
            String cookiesAfter = "";

            do
            {
                try
                {
                    using (var webClient = GetWebClient(headers))
                    {
                        cookiesBefore = makeDisplayableCookieString(cookies);
                        requestHeaders = webClient.Headers.ToString();
                        response = webClient.DownloadString(target);
                        cookiesAfter = makeDisplayableCookieString(cookies);
                        responseHeaders = webClient.ResponseHeaders.ToString();
                        return response;
                    }
                }
                catch (WebException e)
                {
                    if (e.Response != null && 
                        ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.Unauthorized &&
                        connectionInfo.authType != AuthorizationType.Basic)
                    {
                        doSSOAuth();
                        continue;
                    }
                    throw;
                }
                finally
                {
                    Trace.TraceInformation("Get ({0}):\r\n{1}\r\nRequest Headers:\r\n{2}Cookies Before:\r\n{3}Response Headers:\r\n{4}Cookies After:\r\n{5}Response Data\r\n{6}",
                                           DateTime.Now.Subtract(startTime).ToString(),
                                           target.ToString(),
                                           requestHeaders,
                                           cookiesBefore,
                                           responseHeaders,
                                           cookiesAfter,
                                           response);
                }
            } while (true);
        }

        public string Delete(Uri target, IDictionary<string, string> headers = null)
        {
            String response = "<No response>";
            DateTime startTime = DateTime.Now;
            String requestHeaders = "";
            String responseHeaders = "";
            String cookiesBefore = "";
            String cookiesAfter = "";

            do
            {
                try
                {
                    var request = WebRequest.Create(target) as HttpWebRequest;
                    request.Method = "DELETE";
                    request.CookieContainer = cookies;
                    request.Credentials = credentials;
                    if (headers != null)
                    {
                        foreach (var pairs in headers)
                        {
                            request.Headers.Add(pairs.Key, pairs.Value);
                        }
                    }
                    cookiesBefore = makeDisplayableCookieString(cookies);
                    requestHeaders = request.Headers.ToString();
                    var httpResponse = (HttpWebResponse)request.GetResponse();
                    cookiesAfter = makeDisplayableCookieString(cookies);
                    responseHeaders = httpResponse.Headers.ToString();
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        doSSOAuth();
                        continue;
                    }
                    var enc = Encoding.ASCII;
                    var responseStream = new StreamReader(httpResponse.GetResponseStream(), enc);
                    response = responseStream.ReadToEnd();
                    responseStream.Close();
                    return response;
                }
                finally
                {
                    Trace.TraceInformation("Delete ({0}):\r\n{1}\r\nRequest Headers:\r\n{2}Cookies Before:\r\n{3}Response Headers:\r\n{4}Cookies After:\r\n{5}Response Data\r\n{6}",
                                           DateTime.Now.Subtract(startTime).ToString(),
                                           target.ToString(),
                                           requestHeaders,
                                           cookiesBefore,
                                           responseHeaders,
                                           cookiesAfter,
                                           response);
                }            
            } while (true);
        }

        private String makeDisplayableCookieString(CookieContainer cookieContainer)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Cookie cookie in getAllCookies(cookieContainer))
            {
                sb.AppendFormat("Name = {0}\nValue = {1}\nDomain = {2}\n\n",
                    cookie.Name,
                    cookie.Value,
                    cookie.Domain);
            }
            return sb.ToString();
        }

        private CookieCollection getAllCookies(CookieContainer cookieJar)
        {
            CookieCollection cookieCollection = new CookieCollection();

            Hashtable table = (Hashtable)cookieJar.GetType().InvokeMember("m_domainTable",
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.GetField |
                                                                            BindingFlags.Instance,
                                                                            null,
                                                                            cookieJar,
                                                                            new object[] { });

            foreach (var tableKey in table.Keys)
            {
                String str_tableKey = (string)tableKey;

                if (str_tableKey[0] == '.')
                {
                    str_tableKey = str_tableKey.Substring(1);
                }

                SortedList list = (SortedList)table[tableKey].GetType().InvokeMember("m_list",
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.GetField |
                                                                            BindingFlags.Instance,
                                                                            null,
                                                                            table[tableKey],
                                                                            new object[] { });

                foreach (var listKey in list.Keys)
                {
                    String url = "https://" + str_tableKey + (string)listKey;
                    cookieCollection.Add(cookieJar.GetCookies(new Uri(url)));
                }
            }

            return cookieCollection;
        }
    }
}
