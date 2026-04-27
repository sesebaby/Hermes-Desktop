user：postgres
password:zy123good
port：5432

### 以下是云逸中转商的资料
modelname:gemini-2.5-flash-lite-preview-06-17

url:https://api.vectorengine.ai

api_key:sk-SRPBO6gduwIRAQUMyOoCaxGTqrWtdKcseKZkUNPvXaChSdPM

openai：/v1/chat/completions


## 以下是codex中转商资料

base_url = "https://newapi.168box.cn/v1"
api_key="sk-ZRKX3OuOCacicdRZ0MwBfoN5LFTWiXXgwpX1AvQqb2JxRman"
model1="gpt-5.4" 
model2="gpt-5.3" 



## 以下是豆包资料

model:doubao-seed-1-6-flash-250828
apikey:bf4f3ff9-dd87-4bf8-815d-d027399dc249
url:https://ark.cn-beijing.volces.com/api/v3


## 以下是阿里云资料
apikey：sk-d125808070ab45d3a439f779481d2f7b
model：qwen3.6-flash 或者 vanchin/deepseek-v3.2-think(关闭思考)
url:https://dashscope.aliyuncs.com/compatible-mode/v1


示例：
from openai import OpenAI
import os

client = OpenAI(
    # 如果没有配置环境变量，请用阿里云百炼API Key替换：api_key="sk-xxx"
    api_key=os.getenv("DASHSCOPE_API_KEY"),
    base_url="https://dashscope.aliyuncs.com/compatible-mode/v1",
)

messages = [{"role": "user", "content": "你是谁"}]
completion = client.chat.completions.create(
    model="qwen3.5-flash",  # 您可以按需更换为其它深度思考模型
    messages=messages,
    extra_body={"enable_thinking": True},
    stream=True
)
is_answering = False  # 是否进入回复阶段
print("\n" + "=" * 20 + "思考过程" + "=" * 20)
for chunk in completion:
    delta = chunk.choices[0].delta
    if hasattr(delta, "reasoning_content") and delta.reasoning_content is not None:
        if not is_answering:
            print(delta.reasoning_content, end="", flush=True)
    if hasattr(delta, "content") and delta.content:
        if not is_answering:
            print("\n" + "=" * 20 + "完整回复" + "=" * 20)
            is_answering = True
        print(delta.content, end="", flush=True)
