   M S B A M L   `   `   `  ����F  BAutoTubeWpf, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null5clr-namespace:AutoTubeWpf.ViewsAutoTubeWpf.Views  5      ?$clr-namespace:AutoTubeWpf.ViewModelsAutoTubeWpf.ViewModels  5      $    AutoTubeWpf.Views.MetadataView   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      1localclr-namespace:AutoTubeWpf.Views     5      ;
viewmodels$clr-namespace:AutoTubeWpf.ViewModels     5      .��5      � $��15q�6     � RowDefinitions  5   
   �� 5      $
��Auto �6   �� 5      $��* �6   .+�5   
   �� $#��Context / Topic / Description��6   $��0��6:   $��10q�6G   $��0,0,0,10q�6T   .��5      ��  �� AcceptsReturn$
 True��5       �� TextWrapping$
 Wrap=�6+   " �� VerticalScrollBarVisibility$
 Auto=�6?   $��80��5       �� 	IsEnabled$
 True��6%    � Language$ en-US�6A   ��5      �� *ContextText+ �� UpdateSourceTrigger$ PropertyChanged=�.��5   R   .��5   I   5      5      � 5   
   $��1��6    � ColumnDefinitions 5      �� 5      $��* �6#   �� 5      $��* �6#   �� 5       $��* �6#   .+�5$      �� $��Hashtags��6   $��0��6)   $��10q�69   $��0,0,5,0q�6F   .��5%      �   5&      �� 5'      $
��Auto �6(   �� 5(      $��* �6(   �� 5)      $
��Auto �6(   .+�5-      �� $��0��6!    �� Orientation$ 
Horizontal=�6.   $��0,0,0,10q�6G   .+�5.      �� $��Count:��6$   $��Center=�62   $��0,0,5,0q�6M   �� 5/      $��80��6e   ��6"   �� *HashtagCount+$ PropertyChanged=�6e   50      �� 53      $��1��6   	 �� 
IsReadOnly$
	 True��6K   $
 Wrap=�6]   $
 Auto=�6q   ��6+   �� *HashtagsOutput+.��6�   .��54   E   55      �� 58      $��2��6   $��Generate Hashtags��6*   $��0,10,0,0q�59      $��Center=�6/   ��58   F   �� *GenerateHashtagsAsyncCommand+{�6w   �� *CanGenerateMetadata+59   /   5;      5<      �� 5?      $��Tags / Keywords��6   $��1��60   $��10q�6@   $��5,0,5,0q�6M   .��5@      �   5A      �� 5B      $
��Auto �6(   �� 5C      $��* �6(   �� 5D      $
��Auto �6(   .+�5H      �� $��0��6!   $ 
Horizontal=�6.   $��0,0,0,10q�6G   .+�5I      �� $��Count:��6$   $��Center=�62   $��0,0,5,0q�6M   �� 5J      $��80��6a   ��6"   �� *
TagCount+$ PropertyChanged=�6a   5K      �� 5N      $��1��6   $
	 True��6G   $
 Wrap=�6Y   $
 Auto=�6m   ��6+   �� *
TagsOutput+.��6�   .��5O   E   5P      �� 5S      $��2��6   $��Generate Tags��6*   $��0,10,0,0q�5T      $��Center=�6/   ��5S   B   �� *GenerateTagsAsyncCommand+{�6o   �� *CanGenerateMetadata+5T   /   5V      5W      �� 5Z      $��Titles / Headlines��6   $��2��63   $��10q�6C   $��5,0,0,0q�6P   .��5[      �   5\      �� 5]      $
��Auto �6(   �� 5^      $��* �6(   �� 5_      $
��Auto �6(   .+�5c      �� $��0��6!   $ 
Horizontal=�6.   $��0,0,0,10q�6G   .+�5d      �� $��Count:��6$   $��Center=�62   $��0,0,5,0q�6M   �� 5e      $��80��6c   ��6"   �� *
TitleCount+$ PropertyChanged=�6c   5f      �� 5i      $��1��6   $
	 True��6I   $
 Wrap=�6[   $
 Auto=�6o   ��6+   �� *TitlesOutput+.��6�   .��5j   E   5k      �� 5n      $��2��6   $��Generate Titles��6*   $��0,10,0,0q�5o      $��Center=�6/   ��5n   D   �� *GenerateTitlesAsyncCommand+{�6s   �� *CanGenerateMetadata+5o   /   5q      5r      5t      5v      5w      