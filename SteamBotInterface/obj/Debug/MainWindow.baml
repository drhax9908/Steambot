   M S B A M L   `   `   `  ����  SteamBotInterface"    SteamBotInterface.MainWindow   5   	   W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   -   5          �Name$  WindowInterface��5   	    � Title$ 
MainWindow��5   	   $	��424��6   $	��663��6)    � Icon$ assets/icon.png��65   ��5      �� 5   
    �� ImageSource$ assets/background.png��6   .��5      �  � ColumnDefinitions 5	   
   �� 5
      .+�5   
   �� -   5         ��Name$ 
lblBotName��5      $��Loading bot...��6$   $��Center=�6=   $��Center=�6Z   $��	10,10,0,0q�6~   $	��Top=�6�   $	��497��6�   $��36(�6�   $��Italic$�6�   
�������6�   �� 5   
   $��Status :��6   $
��Left=�6#   $��	10,68,0,0q�6>   $	��Top=�6Q   $��85��6i   
�������6t   �� 5   
   -   5        $ 	lblStatus��5      $�� ��6#   $
��Left=�6.   $��
110,68,0,0q�6I   $	��Top=�6]   $	��253��6u   
�������6�   �� 5   
   $��Admins :��6   $
��Left=�6#   $��	10,99,0,0q�6>   $	��Top=�6Q   $��85��6i   
�������6t   �� 5   
   -   5        $ 	lblAdmins��5      $�� ��6#   $
��Left=�6.   $��
110,99,0,0q�6I   $	��Top=�6]   $	��253��6u   
�������6�   �� 5   
   $��API Key��6   $
��Left=�6"   $��380,361,0,0q�6=   $	��Top=�6R   $��53��6j   $��10(�6u   �� 5   
   -   5        $ 	lblApiKey��5      $�� ��6#   $
��Left=�6.   $��433,361,0,0q�6I   $	��Top=�6^   $	��212��6v   $��Italic$�6�   $��10(�6�   �� 5   
   $��Main log file��6   $
��Left=�6(   $��
15,361,0,0q�6C   $	��Top=�6W   $��85��6o   $��10(�6z   �� 5   
   -   5        $ 
lblLogFile��5      $�� ��6$   $
��Left=�6/   $��110,130,0,0q�6J   $	��Top=�6_   $	��253��6w   
�������6�   �� 5   
   -   5        $ lblControlClass��5      $�� ��6)   $
��Left=�64   $��110,160,0,0q�6O   $	��Top=�6d   $	��291��6|   
�������6�   �� 5   
   $��Control class :��6   $
��Left=�6*   $��
10,159,0,0q�6E   $	��Top=�6Y   $��85��6q   
�������6|   �� 5   
   -   5        $ lblMainLogFile��5      $�� ��6(   $
��Left=�63   $��110,361,0,0q�6N   $	��Top=�6c   $	��253��6{   $��10(�6�   �� 5   
   $��
Log file :��6   $
��Left=�6%   $��
10,130,0,0q�6@   $	��Top=�6T   $��86��6l    J� RenderTransformOrigin$ 0.5,0.5&�6w   
�������z�5      V� 5      .~�5      �� �� 5      �� 5       �� Angle$ 0.434Q�6&   T� 5      5      5!      �� 5"   
   -	   5         ��Name$ btnPreviousBot��5"      $��Previous bot��6)   $
��Left=�6@   $��
15,310,0,0q�6[   $	��Top=�6o   $��85��6�   $��24��6�   {�.  6�   �� 5#   
   -
   5        $ 
btnNextBot��5#      $��Next bot��6%   $
��Left=�68   $��546,310,0,0q�6S   $	��Top=�6h   $��85��6�   $��24��6�   {�.  6�   �� 5$   
   $��SteamGuard :��6   $
��Left=�6'   $��
10,186,0,0q�6B   $	��Top=�6V   $��80��6n   
�������6y   �� 5%   
   -   5        $ lblRequireSteamGaurd��5%      $�� ��6.   $
��Left=�69   $��110,186,0,0q�6T   $	��Top=�6i   $	��253��6�   
�������6�   �� 5&   
   $��
Database :��6   $
��Left=�6%   $��
10,217,0,0q�6@   $	��Top=�6T   $��80��6l   
�������6w   �� 5'   
   -   5        $ lblDatabaseName��5'      $�� ��6)   $
��Left=�64   $��110,217,0,0q�6O   $	��Top=�6d   $	��253��6|   
�������6�   5)      5*      