# UNIArt 资源共创平台

## [查看文档](http://wiki.andcrane.com:5152/zh/UNIArt)

## 版本更新记录

- v1.1.9 (2025.7.9)
  - 新增版本记录显示
  - 新增菜单 日志记录查询/清理/解决
  
- v1.1.8 (2025.7.8)
  - 新增层次视图预制体图标
  - 新增模板已安装图标显示

- v1.1.7 (2025.1.22)
  - 新增预制体自动生成预览图
  - 新增模板资源还原更改功能
  - 优化项目资源页面/组件图标
  - 优化资源库安装/卸载在手动重载域模式下的自动重载域逻辑
  
- v1.1.6 (2024.12.04)
  - 调整层次视图菜单结构
  - 增加按钮/开关相互转化功能
  - 层次视图部分菜单支持多选批量处理
  
- v1.1.5 (2024.11.29)
  - 非本地项目及基础组件库，默认资源筛选项改为Prefab
  - 筛选目标在指定文件夹下没有资源时，不显示该文件夹
  - 拖拽资源到UNIArt工作台时，自适应资源筛选类型
  
- v1.1.4
  - 修复偶尔无法拖拽外部资源的bug
  - 增加拖拽单张图集序列图生成动画的能力
  - 兼容2023.1

- v1.1.3
  - 优化Animator预览控制逻辑
  - 保持动画预览选项，保留动画名及动画帧数
  
- v1.1.2
  - 修复psd文件名中包含.psd时，导入失败的bug
  - 优化ToggleImage组件，切换图片时自动匹配原始图片尺寸
  - 增加Animation预览功能

- v1.1.1
  - 修复Standard库安装状态刷新错误
  - 优化调试信息显示
  - svn 增加删除接口
  - 协同CollabHub#Standard.Editor增加 Make Pure Project选项

- v1.1.0
  - 修复 @点击 @选中， ps图层不可见时的bug
  - 优化@选中，与ps端一致性
  - 增加本地预制体预览功能一致性

- v1.0.11-preview.10
  - 正式命名模板库为CollabHub
  - 完成模板库更目录版本控制自动化
  
- v1.0.11-preview.9
  - 修复选中多个图片，拖拽生成动画时不必要的报错
  - 循环动画复选框
  - 模板库存放位置调整->Packages
  
- v1.0.11-preview.8
  - 修复图层非法字符命名导致的报错
  - 修复图层组中包含@默认关键字导致的报错
  - 优化@默认 包含空格的处理
  - 修复filter重命名bug，重命名至所有合法路径
  - 修复资源库安装卸载自动定位bug
  - 优化层次视图图标显示，空间不够时不显示
  
- v1.0.11-preview.7
  - 修复序列图拖拽生成动画bug
  
- v1.0.11-preview.1 & 2 & 3 & 4 & 5 & 6
  - 增加新建文件夹功能
  - 优化界面布局
  - 优化提交逻辑，修复资源库两步提交冗余问题
  - 优化升级提示
  - 优化模板库名称显示
  - 修复多个预制体拖拽到图层bug
  - 新增持久化筛选器的选中状态
  - 优化模板列表/筛选器新建/资源选择 自动定位到选择位置
  
- v1.0.10
  - 修复2021.3.15 ps选项报错
  - 优化图层命名 @默认 @点击 @选中
  
- v1.0.9
  - 优化多个PSD文件导入逻辑
  - 增加移除PS组件能力
  - 增加PS导入选项
  - 增加拖拽面板引导
  - 优化资源菜单显示
  - 增加PSD导入后处理，恢复副本功能
  - 新建动画控制器，默认添加动画 [出现]

- v1.0.8
  - 优化PSD自动导入流程，确保不再导入hook中调用相关禁止调用的编辑器API
  - 增加资源选择操作快捷键, 移动选择，改名，删除

- v1.0.7
  - 增加文件/筛选器 重命名/删除功能
  - 优化依赖关系处理逻辑
  - PSD导入逻辑优化文件夹
  - 拖拽逻辑优化
  
- v1.0.6
  - 多文件拖拽生成序列帧动画
  - 增加层次视图选项菜单
  - 场景GameObject拖拽视图，保存为预制体
  - 资源右键菜单挪至全局视图
  - 增加提交/更新按钮，优化样式
  - 多资源选择能力 
  - 模板列表页优先级
  
- v1.0.6-preview.10
  - 增加文件导入功能
  - 增加PSD文件支持
  
- v1.0.6-preview.9
  - 增加本地项目分类
  - 增加图片类资源浏览
  - 增加资源筛选功能
  - 优化筛选标签显示
  - 增加递归筛选文件夹功能
  - 优化页面布局
  
- v1.0.6-preview.8 (2024.09.20)
  - Standard 默认安装可配置
  - 工作台<-->项目文件夹相互拖拽逻辑优化依赖处理以及逻辑验证

- v1.0.6-preview.7 (2024.09.19)
   - 拖上去再拖回来，子路径问题，会直接拖拽到根目录
   - 切换模版保留筛选路径 
   - SVN菜单功能对模板进行支持
   - 快捷键刷新资源视图 
   - 拖拽到模板资源自动处理资源依赖
   - 优化拖拽到层次视图的逻辑

- v1.0.6-preview.6 (2024.09.15)
  - 项目资源可拖拽到工作台
  - 优化资源拖拽体验
  - 资源拖拽自动处理依赖
  - 预览图显示位置优化
  
- v1.0.6-preview.4&5 
  - 优化UNIArt设置项
  - 修复Odin依赖bug

- v1.0.6-preview.3
  - 模板仓库携带版本信息，防止更新破坏工程
  - 优化程序集定义
  - 新增UNIArt设置项

- v1.0.6-preview.1&2 (2024.09.07)
  - 增加UNIArt 控制台
  - 实现通用组件独立版本控制
  - 形成 UNIArt 完整工作流

- v1.0.5_1 (2024.08.30)
  - 移除多余命名空间Odin
  
- v1.0.5 (2024.08.29)
  - 增加 SVN 修改仓库名 选项
  - 增加 SVN 切换仓库源 选项
- v1.0.4 (2024.07.30)
  - 优化SVN仓库地址URL解码
  
- v1.0.3 (2024.07.28)
  - 增加SVN工具菜单选项
  
- v1.0.2 (2024.07.18)
  - 移除UIPreviewer
  - 修改Animator控制器默认名称
  
- v1.0.1 (2024.05.22)
  - 增加R2D && UIPreviewer并进行UNIArt兼容 与 汉化
- v1.0.0 (2023.03.15)
  - 自动设置TMP默认字体为ArtAssets/Fonts下命名为DefaultTMPFont的字体
  - Animtor组件拓展, 可进行动画编辑拓展
  - UI Page页面创建自动化
  - 美术资源目录结构自动化