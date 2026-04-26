# Launcher Account Surface State Machine

状态：

- active design baseline

owner：

- launcher product owner

用途：

- 用大白话写死：账号相关页面要怎么切，玩家在桌面端什么时候看到登录、过期、失败、已登录。

固定回链：

- `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`

surface owner：

- `Launcher`

固定页面状态：

1. `account_entry`
2. `register_form`
3. `login_form`
4. `auth_loading`
5. `account_home`
6. `session_expired_notice`
7. `auth_error`

状态迁移：

- `account_entry -> register_form`
- `account_entry -> login_form`
- `register_form -> auth_loading`
- `login_form -> auth_loading`
- `auth_loading -> account_home`
- `auth_loading -> auth_error`
- `account_home -> session_expired_notice`
- `session_expired_notice -> login_form`
- `account_home -> account_entry`（退出登录）

页面最小内容：

1. `account_entry`
   - 注册入口
   - 登录入口
   - 当前能看什么说明
2. `register_form`
   - 注册字段
   - 提交按钮
   - 失败提示位
3. `login_form`
   - 登录字段
   - 提交按钮
   - 失败提示位
4. `account_home`
   - 当前账号名
   - 会话状态
   - 我的权益入口
   - 退出登录入口
5. `session_expired_notice`
   - 过期说明
   - 重新登录按钮
6. `auth_error`
   - 大白话错误说明
   - 重试按钮

死规则：

1. 过期必须单独露面，不允许静默回匿名
2. 登录失败必须留在当前账号面，不允许只弹个瞬时提示
3. 账号面状态变化不能影响游戏本地安装状态显示

绝对禁止：

1. 不允许“未登录但页面像已登录”
2. 不允许会话过期后还继续显示旧权益结果
3. 不允许账号错误直接冒充网络或安装错误

update trigger：

- 账号页面状态变化
- 过期 / 失败处理变化
- 账号面最小内容变化
