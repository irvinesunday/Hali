using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Tests.Unit.Signals;

internal class FakeHttpMessageHandler : HttpMessageHandler
{
	private readonly HttpStatusCode _status;

	private readonly string _body;

	public FakeHttpMessageHandler(HttpStatusCode status, string body)
	{
		_status = status;
		_body = body;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage result = new HttpResponseMessage(_status)
		{
			Content = new StringContent(_body, Encoding.UTF8, "application/json")
		};
		return Task.FromResult(result);
	}
}
