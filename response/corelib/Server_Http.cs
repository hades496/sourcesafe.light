﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace corelib
{
    partial class Server
    {
        System.Net.HttpListener _http_listener = new System.Net.HttpListener();
        //string _http_basehost = "ss.light";
        #region _http_init
        void _http_init(string url)
        {

            _http_listener.Prefixes.Add(url);
            try
            {
                _http_listener.Start();
                _http_beginRecv();
                logger.Log_Warn("Http on:" + url + config.basehost);
            }
            catch (Exception err)
            {
                logger.Log_Error(err.ToString());
                logger.Log_Error("Http Init fail.");
                return;
            }
            if (System.IO.Directory.Exists("publish") == false)
            {
                System.IO.Directory.CreateDirectory("publish");
            }

        }

        void _http_beginRecv()
        {
            try
            {
                _http_listener.BeginGetContext(_http_onRequest, null);
            }
            catch (Exception err)
            {
                if (exited) return;
                else logger.Log_Error("err");
            }
        }
        void _http_onRequest(IAsyncResult hr)
        {
            _http_beginRecv();
            if (exited) return;
            var reqcontext = _http_listener.EndGetContext(hr);
            if (reqcontext.Request.HttpMethod.ToLower() == "post")
            {
                byte[] buf = new byte[reqcontext.Request.ContentLength64];
                int bufread = 0;

                AsyncCallback onPostReq = null;
                onPostReq = (phr) =>
                     {
                         bufread += reqcontext.Request.InputStream.EndRead(phr);
                         if (bufread == reqcontext.Request.ContentLength64)
                         {
                             _http_OnHttpIn(reqcontext, buf);
                         }
                         else
                         {
                             reqcontext.Request.InputStream.BeginRead(buf, bufread, (int)reqcontext.Request.ContentLength64 - bufread, onPostReq, null);
                         }
                     };
                reqcontext.Request.InputStream.BeginRead(buf, 0, buf.Length, onPostReq, null);
            }
            else
            {
                _http_OnHttpIn(reqcontext, null);
            }
        }
        #endregion
        #region _http_response
        void _http_response(System.Net.HttpListenerContext req, byte[] buf)
        {
            req.Response.ContentEncoding = System.Text.Encoding.UTF8;
            req.Response.ContentLength64 = buf.Length;
            req.Response.ContentType = "application/octet-stream";
            AsyncCallback onResponse = onResponse = (rhr) =>
            {
                req.Response.OutputStream.EndWrite(rhr);
                req.Response.Close();
            };
            req.Response.OutputStream.BeginWrite(buf, 0, buf.Length, onResponse, null);
        }
        void _http_response(System.Net.HttpListenerContext req, string str)
        {
            req.Response.ContentEncoding = System.Text.Encoding.UTF8;
            req.Response.ContentType = "text/plain";
            byte[] bufout = System.Text.Encoding.UTF8.GetBytes(str);
            req.Response.ContentLength64 = bufout.Length;

            AsyncCallback onResponse = onResponse = (rhr) =>
            {
                req.Response.OutputStream.EndWrite(rhr);
                req.Response.Close();
            };
            req.Response.OutputStream.BeginWrite(bufout, 0, bufout.Length, onResponse, null);
        }
        void _http_response_html(System.Net.HttpListenerContext req, string str)
        {
            req.Response.ContentEncoding = System.Text.Encoding.UTF8;
            req.Response.ContentType = "text/html";
            byte[] bufout = System.Text.Encoding.UTF8.GetBytes(str);
            req.Response.ContentLength64 = bufout.Length;

            AsyncCallback onResponse = onResponse = (rhr) =>
            {
                req.Response.OutputStream.EndWrite(rhr);
                req.Response.Close();
            };
            req.Response.OutputStream.BeginWrite(bufout, 0, bufout.Length, onResponse, null);
        }


        #endregion
        #region _http_quit
        void _http_Stop()
        {
            _http_listener.Abort();
        }
        #endregion

        void _http_OnHttpIn(System.Net.HttpListenerContext req, byte[] postdata)
        {
            try
            {
                if (req.Request.Url.AbsolutePath.IndexOf("/publish") == 0)
                {
                    _http_download(req);
                    return;
                }

                if (req.Request.Url.AbsolutePath == "/" + config.basehost)
                {
                    string strreturn = null;//统一json返回串
                    switch (req.Request.QueryString["c"])
                    {
                        case "get"://Get返回二进制格式
                            _http_cmd_get(req);
                            return;
                        case "rpc"://Get返回二进制格式
                            strreturn = _http_cmd_rpc(req,postdata);
                            break;
                        case "play":
                            strreturn = _http_cmd_play(req);
                            break;
                        case "ping":
                            strreturn = _http_cmd_ping(req);
                            break;
                        case "login":
                            strreturn = _http_cmd_login(req);
                            break;

                        default:
                            _http_cmd_help(req);//Help返回html
                            return;
                    }
                    _http_response(req, strreturn);
                    //command
                    return;
                }
                else
                {
                    _http_response_html(req, "<html>not found page.</html>");

                }

            }
            catch (Exception err)
            {
                _http_response(req, err.ToString());
            }
        }
        void _http_cmd_help(System.Net.HttpListenerContext req)
        {
            string helpstr =
@"<html>
<head> <meta charset=""UTF-8""></head>
<a>FORALL 00 ?c=play try to get the gamelist</a><br/>
<hr/>
<a>FORALL 01 ?c=ping test that if this server is a sourcesafe.light server or not.</a><br/>
<hr/>
<a>FORALL 02 ?c=login&g=[gamename]&u=[username]&p=[passwod]</a><br/>
<a>                  login for a game</a>        <br/>            
<hr/> 
<a>RPC  ?c=rpc&s=""""<a><br/>
<a>                  call rpc to sync the version</a><br/>           
<hr/>
<a>GET  ?c=get&f=""""&v=""""&p=1or0</a><br/>
<a>下载一个文件</a><br/>
</html>";
            _http_response_html(req, helpstr);
        }

        string _http_cmd_play(System.Net.HttpListenerContext req)
        {//获取GameList
            MyJson.JsonNode_Object map = new MyJson.JsonNode_Object();
            map.SetDictValue("status", 0);
            map["list"] = new MyJson.JsonNode_Array();
            var list = map["list"].AsList();
            foreach (var g in config.games)
            {
                MyJson.JsonNode_Object gameitem = new MyJson.JsonNode_Object();
                gameitem.SetDictValue("name", g.name);
                gameitem.SetDictValue("desc", g.desc);
                gameitem.SetDictValue("admin", g.admin);
                list.Add(gameitem);
            }

            return map.ToString();
        }
        string _http_cmd_ping(System.Net.HttpListenerContext req)
        {//固定协议
            MyJson.JsonNode_Object map = new MyJson.JsonNode_Object();
            map["status"] = new MyJson.JsonNode_ValueNumber(0);
            map["ver"] = new MyJson.JsonNode_ValueString("V0.01");
            map["sign"] = new MyJson.JsonNode_ValueString("sha1");
            return map.ToString();
        }



        class LoginInfo
        {
            public string username;
            public string token;
            public string game;
        }
        Dictionary<string, LoginInfo> tokens = new Dictionary<string, LoginInfo>();
        string _http_cmd_login(System.Net.HttpListenerContext req)
        {
            MyJson.JsonNode_Object map = new MyJson.JsonNode_Object();
            
            string game = req.Request.QueryString["g"];
            string username = req.Request.QueryString["u"];
            string password = req.Request.QueryString["p"];
            var _user=config.getArtist(username);
            var _game =config.getGame(game);
            if(_user==null)
            {
                map.SetDictValue("status", -1001);
                map.SetDictValue("msg", "user not exist.");
                return map.ToString();
            }
            if(_user.code!=password)
            {
                map.SetDictValue("status", -1002);
                map.SetDictValue("msg", "user password is error.");
                return map.ToString();
            }
            if(_game==null)
            {
                if (_user.level > 10)
                {
                    if (System.IO.Directory.Exists("games/" + game) == false)
                    {
                        System.IO.Directory.CreateDirectory("games/" + game);
                    }
                    if(System.IO.Directory.Exists("games/" + game))
                    {
                        _game = new ServerConfig.Game();
                        _game.name = game;
                        _game.desc = "";
                        _game.admin = username;
                        _game.users.Add(username);
                        config.games.Add(_game);
                        config.Save();
                    }
                }
                if(_game==null)
                {
                    map.SetDictValue("status", -1003);
                    map.SetDictValue("msg", "game not exist.when superadmin Login to a empty game will create one.");
                    return map.ToString();
                }
            }

            if (_game.users.Contains(username) == false)
            {
                map.SetDictValue("status", -1004);
                map.SetDictValue("msg", "you can not edit this game.");
                return map.ToString();
            }

            LoginInfo info = new LoginInfo();
            info.game = game;
            info.username = username;
            info.token = Guid.NewGuid().ToString();
            tokens[info.token] = info;
            map.SetDictValue("status", 0);
            map.SetDictValue("token", info.token);

            if(versions.ContainsKey(game)==false)
            {
                versions[game] = VersionGroup.Load(game,"games/" + game);
            }
            return map.ToString();
        }


    }
}
