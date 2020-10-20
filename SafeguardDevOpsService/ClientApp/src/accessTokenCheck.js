//Checks for an access token in the URL, and if one is found, store it in session storage and
//redirect to the same URL minus query parameters.

//Returns the value of the access token if it is in the current URL, null otherwise
function getAccessToken(){
  var index = this.window.location.href.indexOf("access_token=");
  if (index == -1){
    return null;
  }
  var token = this.window.location.href.substring(index+13);
  var eindex = token.search(/[&#\/]/);
  if (eindex > -1){
    return token.substring(0, eindex);
  }
  return token;
}

var token = this.getAccessToken();
if (token){
  console.log('ACCESS TOKEN ' + token);
  var index = this.window.location.pathname.indexOf('remote-token');
  if (index >= 0){
    this.window.sessionStorage.setItem("RemoteAccessToken", token);
    this.window.location.href = this.window.location.protocol + "//" + this.window.location.host + this.window.location.pathname;
  }
  else{
    this.window.sessionStorage.setItem("AccessToken", token);
    this.window.location.href = this.window.location.protocol + "//" + this.window.location.host + this.window.location.pathname;
  }
}