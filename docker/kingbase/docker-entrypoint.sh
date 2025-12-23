#!/bin/bash

shell_folder=$(dirname $(readlink -f "$0"))

## 选填项：install.conf所在绝对路径
install_conf=""

## 必填项：可以配置在install.conf中
##         配置文件中优先级最高，配置文件中的对应配置会覆盖本脚本中的以下配置

#all_ip=(192.168.28.10 192.168.28.11)
all_ip=()
soft_dir="/home/kingbase/cluster"
kmonitor_dir="/home/kmonitor"
install_dir=""
backup_dir=""

db_user="system"
db_password="123456"
esrep_password="Kingbaseha110"
db_port="54321"
db_mode="oracle"
db_encoding="utf-8"
db_case_sensitive="yes"
db_auth="md5"
initdb_options=""

#trusted_servers="192.168.28.1"
trusted_servers=""

## 选填项
reconnect_attempts="5"   #重试次数，检查数据库失败后重试
reconnect_interval="6"   #重试间隔

execute_user="kingbase"  #普通用户，一般为kingbase; 如果未配置，默认为kingbase
super_user="root"        #超级用户，一般为root; 如果未配置，默认为root用户

#virtual_ip="192.168.28.188/24"
virtual_ip=""
net_device="eth0"
ipaddr_path=""
arping_path=""

repmgrd_pid_file=""
log_file=""

## 默认选项
conninfo=""
sys_bindir=""
data_directory=""
cron_name=""
primary_host=""
backup_host=""

## 默认配置
connection_timeout=10
cron_file="/etc/cron.d/KINGBASECRON"

## 缩容等待时间
shrink_max_wait_time=20

## kmonitor选项
kmonitor_enable=false

function JudgeTodo()
{
    if [ -z $MOUNT_PATH ]
    then
        install_dir="${soft_dir}"
    else
        if [[ $MOUNT_PATH = /home/* ]]
        then
            echo "can not set MOUNT_PATH with \"/home/\""
            exit 0
        fi
        install_dir="$MOUNT_PATH"
    fi
    data_directory="${install_dir}/data"
    backup_dir="${install_dir}/backup_dir"
    if [ "$function_name"x != "shrink"x ] && [ "$function_name"x != "update_heart_beat"x ]
    then
        if [ -d $data_directory ]
        then
            chown -R kingbase. ${install_dir} 2>/dev/null
            echo "This container has own data directory ..."
            if [ ! -f /home/kingbase/.encpwd ]
            then
                echo "Try to copy the ${install_dir}/etc/.encpwd to /home/kingbase/.encpwd ..."
                # 还未读取配置文件，一切参数都需要谨慎使用
                execute_command kingbase localhost "test -f ${install_dir}/etc/.encpwd && cp ${install_dir}/etc/.encpwd /home/kingbase/.encpwd"
                if [ $? -ne 0 ]
                then
                    echo "Try to copy the ${install_dir}/etc/.encpwd to /home/kingbase/.encpwd ... failed"
                else
                    echo "Try to copy the ${install_dir}/etc/.encpwd to /home/kingbase/.encpwd ... OK"
                fi

                echo "Try to chmod 600 the ${install_dir}/etc/.encpwd and /home/kingbase/.encpwd ..."
                # 还未读取配置文件，一切参数都需要谨慎使用
                execute_command kingbase localhost "chmod 600 ${install_dir}/etc/.encpwd && chmod 600 /home/kingbase/.encpwd"
                if [ $? -ne 0 ]
                then
                    echo "Try to chmod 600 the ${install_dir}/etc/.encpwd and /home/kingbase/.encpwd ... failed"
                else
                    echo "Try to chmod 600 the ${install_dir}/etc/.encpwd and /home/kingbase/.encpwd ... OK"
                fi
            fi
            #检查数据库是否启动
            echo "Checking db running,db_port:${db_port}..."
            local db_running=`netstat -apn 2>/dev/null|grep -w "${db_port}"|wc -l`
            if [ $? -ne 0 -o "${db_running}"x == "0"x ]
            then
                echo "[WARNING] the db on \"${ip}:${db_port}\" is not running"
                echo "sys_monitor start local ..."
                execute_command kingbase localhost "${install_dir}/bin/sys_monitor.sh startlocal"
                if [ $? -ne 0 ]
                then
                    echo "sys_monitor start local fail!"
                    exit 0
                fi
                echo "sys_monitor start local ...OK"

                echo "force register..."
                execute_command kingbase localhost "${install_dir}/bin/repmgr standby register -F"
                if [ $? -ne 0 ]
                then
                    echo "force register...fail!"
                    exit 0
                fi
                echo "force register...OK"
                echo "start kmonitor on localhost..."
                start_kmonitor_local localhost
                if [ $? -ne 0 ]
                then
                    echo "start kmonitor on localhost...fail!"
                else
                    echo "start kmonitor on localhost ...OK"
                fi
            fi
            echo "Checking db running ...OK"
            echo "DONE"
            exit 0
        fi
        if [ "$function_name"x != "update_heart_beat"x ]
        then
            if [ -d $backup_dir ]
            then
                chown -R kingbase. ${install_dir} 2>/dev/null
                echo "This container has own backup directory ..."
                if [ ! -f /home/kingbase/.encpwd ]
                then
                    echo "Try to copy the ${install_dir}/etc/.encpwd to /home/kingbase/.encpwd ..."
                    # 还未读取配置文件，一切参数都需要谨慎使用
                    execute_command kingbase localhost "test -f ${install_dir}/etc/.encpwd && cp ${install_dir}/etc/.encpwd /home/kingbase/.encpwd"
                    if [ $? -ne 0 ]
                    then
                        echo "Try to copy the ${install_dir}/etc/.encpwd to /home/kingbase/.encpwd ... failed"
                    else
                        echo "Try to copy the ${install_dir}/etc/.encpwd to /home/kingbase/.encpwd ... OK"
                    fi
                    execute_command kingbase localhost "chmod 600 ${install_dir}/etc/.encpwd && chmod 600 /home/kingbase/.encpwd"
                    if [ $? -ne 0 ]
                    then
                        echo "Try to chmod 600 the ${install_dir}/etc/.encpwd and /home/kingbase/.encpwd ... failed"
                    else
                        echo "Try to chmod 600 the ${install_dir}/etc/.encpwd and /home/kingbase/.encpwd ... OK"
                    fi
                fi
                echo "DONE"
                exit 0
            fi
        fi
    fi
    if [ -z $HOSTNAME ]
    then
        echo "No set HOSTNAME!"
        exit 0
    fi
    if [ "$function_name"x != "update_heart_beat"x ]
    then
        if [ -z $REPLICA_COUNT ]
        then
            echo "No set REPLICA_COUNT"
            exit 0
        #elif [ $REPLICA_COUNT -lt 3 ]
        #then
        #    echo "REPLICA_COUNT can not less than 3"
        #    exit 0
        fi
    fi
    pod_index=`echo ${HOSTNAME: -1}`
    ((rep_num=$REPLICA_COUNT-1))
    if [ "$function_name"x != "update_heart_beat"x ]
    then
        if [ $rep_num -ne $pod_index ]
        then
            echo "Other container index[$rep_num] do init, exit .."
            exit 0
        fi
        echo "This container doing initalization, please hold on ...."
    fi
}
function load_env_file()
{
    #环境变量配置文件存在，则读取配置文件
    if [ "$function_name"x == "update_heart_beat"x ]
    then
        ALL_NODE_IP=`read_conf_value_local "${env_file}" "ALL_NODE_IP"`
        all_ip=($ALL_NODE_IP)
        local i=0
        for ip in ${all_ip[@]}
        do
            if [ "$pod_index"x == "$i"x ]
            then
                echo "[CONFIG] read ${env_file} on $ip ..."
                execute_command kingbase "$ip" "test -f  ${env_file}"
                if [ $? -eq 0 ]
                then
                    while read t_one_line; do
                        local is_ALL_NODE_IP=`echo ${t_one_line} |grep -aE ALL_NODE_IP |wc -l`
                        if [ $is_ALL_NODE_IP -eq 0 ]
                        then
                            eval ${t_one_line}
                        else
                            echo "[INFO] is_ALL_NODE_IP:$is_ALL_NODE_IP t_one_line:$t_one_line"
                            ALL_NODE_IP=`echo ${t_one_line}|awk -F "=" '{print $2}'`
                        fi
                    done < ${env_file}
                    echo "[CONFIG] read ${env_file} on $ip ...OK"
                else
                    echo "[WARNING] ${env_file} not exist on ${ip}"
                    echo "[CONFIG] read ${env_file} on $ip ...fail"
                fi
            fi
            let i++
        done
    fi
}
function load_repmgr_config()
{
    #未配置repmgr.conf路径，设置为当前路径的 ../etc/repmgr.conf
    [ "${rep_conf}"x = ""x ] && rep_conf="${shell_folder}/../etc/repmgr.conf"

    #配置文件不存在，报错返回
    if [ ! -f $rep_conf ]
    then
        echo "the config file \"${rep_conf}\" is not exists"
        return 1
    fi
    #配置都已配好，不需要读取配置文件
    #[ "${sys_bindir}"x != ""x -a "${data_directory}"x != ""x -a "${conninfo}"x != ""x -a "${repmgrd_pid_file}"x != ""x ] && return 0

    while read cfg
    do
        # del comment in cfg
        param=${cfg%%#*}
        paramName=${param%%=*}
        paramValue=${param#*=}
        if [ -z "$paramName" ] ; then
            continue
        elif [ -z "$paramValue" ]; then
            continue
        fi
        eval $paramName="${paramValue}"
    done < $rep_conf

    [ "${repmgrd_pid_file}"x = ""x ] && repmgrd_pid_file="/tmp/repmgrd.pid"
    [ "${es_user}"x = ""x ] && es_user="kingbase"
    [ "${es_password}"x = ""x ] && es_password="123456"
    [ "${es_port}"x = ""x ] && es_port=8890
    [ "${lsn_lag_threshold}"x = ""x ] && lsn_lag_threshold=16

    if [ "${sys_bindir}"x = ""x ]
    then
        echo "param [sys_bindir] is not set in config file \"${rep_conf}\""
        return 1
    elif [ "${data_directory}"x = ""x ]
    then
        echo "param [data_directory] is not set in config file \"${rep_conf}\""
        return 1
    elif [ "${conninfo}"x = ""x ]
    then
        echo "param [conninfo] is not set in config file \"${rep_conf}\""
        return 1
    fi
    return 0
}
function load_config()
{

    #install.conf路径，设置为当前路径的 ./install.conf
    [ "${install_conf}"x = ""x ] && install_conf="${shell_folder}/install.conf"
    #未配置repmgr.conf路径，设置为当前路径的 ../etc/repmgr.conf
    [ "${rep_conf}"x = ""x ] && rep_conf="${shell_folder}/../etc/repmgr.conf"
    if [ "$function_name"x == "update_heart_beat"x ]
    then
        [ "${env_file}"x = ""x ] && env_file="${shell_folder}/../etc/envfile"
    else
        [ "${env_file}"x = ""x ] && env_file="${install_dir}/etc/envfile"
    fi


    #配置文件存在，则读取配置文件
    #if [ -f $install_conf ]
    #then
    #    load_conf_value_under_header $install_conf install
    #fi

    if [ "${ALL_NODE_IP}"x != ""x ]
    then
        all_ip=($ALL_NODE_IP)
    fi
    # 定时任务需要加载环境变量配置文件
    load_env_file

    #获取环境变量配置
    if [ "${TRUST_IP}"x != ""x ]
    then
        trusted_servers="$TRUST_IP"
    fi
    if [ "${SHRINK_MAX_WAIT_TIME}"x != ""x ] && [ ${SHRINK_MAX_WAIT_TIME} -gt $shrink_max_wait_time ]
    then
        shrink_max_wait_time=($SHRINK_MAX_WAIT_TIME)
    fi
    if [ "${DB_VIP}"x != ""x ]
    then
        virtual_ip=($DB_VIP)
    fi
    if [ "${DB_USER}"x != ""x ]
    then
        db_user="${DB_USER}"
    fi
    if [ "${DB_PASSWORD}"x != ""x ]
    then
        db_password="${DB_PASSWORD}"
    fi
    if [ "${ESREP_PASSWORD}"x != ""x ]
    then
        esrep_password="${ESREP_PASSWORD}"
    fi
    if [ "${DB_MODE}"x != ""x ]
    then
        db_mode="${DB_MODE}"
        initdb_options="${initdb_options} -m $db_mode"
    fi
    if [ "${DB_ENCODING}"x != ""x ]
    then
        db_encoding="${DB_ENCODING}"
        initdb_options="${initdb_options} -E ${db_encoding}"
    fi
    if [ "${DB_CASE_SENSITIVE}"x == "no"x ]
    then
        initdb_options="${initdb_options} --enable-ci"
    fi
    if [ "${usersetip}"x != ""x ]
    then
        all_ip=(${usersetip})
    fi

    if [ "${KMONITOR_SERVER_ADDRESS}"x != ""x ]
    then
        kmonitor_server_address="${KMONITOR_SERVER_ADDRESS}"
    fi
    if [ "${KMONITOR_ENABLE}"x == "true"x ]
    then
        kmonitor_enable="${KMONITOR_ENABLE}"
    fi

    if [ "${all_ip}"x = ""x ]
    then
        echo "[CONFIG_CHECK] param ALL_NODE_IP(all_ip) is not set function_name:$function_name ALL_NODE_IP:$ALL_NODE_IP"
        return 1
    elif [ "${#all_ip[@]}"x != "$REPLICA_COUNT"x ]
    then
        echo "[CONFIG_CHECK] the count of ALL_NODE_IP(all_ip):${#all_ip[@]} is not equal to REPLICA_COUNT:$REPLICA_COUNT"
        return 1
    fi
    if [ "${install_dir}"x = ""x ]
    then
        echo "[CONFIG_CHECK] param (install_dir) is not set"
        return 1
    elif [ "${trusted_servers}"x = ""x ]
    then
        echo "[CONFIG_CHECK] param TRUST_IP(trusted_servers) is not set"
        return 1
    fi
    return 0
}

function load_conf_value_under_header()
{
    local conf_path=$1
    local target_header=\[$2\]
    local read_flag=0
    if [ -f $conf_path ]
    then
        while read t_one_line ;
        do
            local is_header=`echo "$t_one_line" |  egrep '^\[.*]' | wc -l`
            if [ $is_header -eq 1 ]
            then
                if [ x"$target_header" == x"$t_one_line" ]
                then
                    read_flag=1
                else
                    read_flag=0
                fi
           fi
           if [ $read_flag -eq 1 -a $is_header -eq 0 ]
           then
               eval ${t_one_line} ;
           fi
        done < $conf_path
    fi
}

function pre_exe()
{
    load_config
    [ $? -ne 0 ] && exit 0
    check_and_get_user

    [ "${db_port}"x = ""x ] && db_port="54321"
    [ "${db_auth}"x = ""x ] && db_auth="trust"
    [ "${reconnect_attempts}"x = ""x ] && reconnect_attempts="5"
    [ "${reconnect_interval}"x = ""x ] && reconnect_interval="6"
    [ "${sys_bindir}"x = ""x ] && sys_bindir="${install_dir}/bin"
    [ "${data_directory}"x = ""x ] && data_directory="${install_dir}/data"
    [ "${repmgrd_pid_file}"x = ""x ] && repmgrd_pid_file="${sys_bindir}/hamgr.pid"
    [ "${log_file}"x = ""x ] && log_file="${install_dir}/hamgr.log"
    [ "${kbha_log_file}"x = ""x ] && kbha_log_file="${install_dir}/kbha.log"
    [ "${conninfo}"x = ""x ] && conninfo="user=esrep dbname=esrep port=${db_port} connect_timeout=10"
    [ "${repmgr_conf}"x = ""x ] && repmgr_conf=${install_dir}/etc/repmgr.conf
    if [ "${db_user}"x = ""x ]
    then
        echo "[CONFIG_CHECK] the value of \"db_user\" can not be NULL, please check it"
        exit 0
    fi

    if [ "${db_auth}"x != "trust"x -a "${db_auth}"x != "md5"x -a "${db_auth}"x != "scram-sha-256"x ]
    then
        echo "[CONFIG_CHECK] the value of \"db_auth\" can only set as \"trust\" or \"md5\" or \"scram-sha-256\" "
        exit 0
    fi
    if [ "${db_auth}"x != "trust"x -a "${db_password}"x = ""x ]
    then
        echo "[CONFIG_CHECK] the value of \"db_password\" can not be NULL, please check it"
        exit 0
    fi

    if [ "${db_mode}"x = ""x ]
    then
        echo "[CONFIG_CHECK] the value of \"db_mode\" can not be NULL, please check it"
        exit 0
    fi
    if [ "${db_encoding}"x = ""x ]
    then
        echo "[CONFIG_CHECK] the value of \"db_encoding\" can not be NULL, please check it"
        exit 0
    fi
    if [ "${cron_name}"x == ""x ]
    then
        which crond > /dev/null 2>&1
        if [ $? -eq 0 ]
        then
            # Now ,we have used these system successfully e.g centos6.x centos7.x redhat.
            cron_name="crond"
        else
            which cron > /dev/null 2>&1
            if [ $? -eq 0 ]
            then
                ## Now ,we have used these system successfully e.g Deepin .
                cron_name="cron"
            else
                echo "Don't know the crontab service name."
            fi
        fi
    fi

    if [ "${virtual_ip}"x != ""x ]
    then
        vip_ip=${virtual_ip%/*}
        [ "${ipaddr_path}"x = ""x ] && ipaddr_path="/sbin"
        [ "${arping_path}"x = ""x ] && arping_path="/sbin"

        if [ ! -f "${ipaddr_path}/ip" ]
        then
            echo "[CONFIG_CHECK] the dir \"${ipaddr_path}\" has no execute file \"ip\", please set [ipaddr_path]"
            exit 0
        elif [ ! -f "${arping_path}/arping" ]
        then
            echo "[CONFIG_CHECK] the dir \"${arping_path}\" has no execute file \"arping\", please set [arping_path]"
            exit 0
        elif [ "${net_device}"x = ""x ]
        then
            echo "[CONFIG_CHECK] \"net_device\" is NULL, please check it"
            exit 0
        fi

        echo "[CONFIG_CHECK] check the virtual ip \"${vip_ip}\" is already exist ..."
        local is_vip_exist=`ping ${vip_ip} -c 3 -w 3 | grep received | awk '{print $4}'`
        if [ $? -ne 0 ] || [ $is_vip_exist -gt 0 ]
        then
            echo "[CONFIG_CHECK] `date +'%Y-%m-%d %H:%M:%S'` The virtual ip [${virtual_ip}] has already exists, exit."
            exit 0
        fi
        echo "[CONFIG_CHECK] there is no \"${vip_ip}\" on any host, OK"
    fi
}

function check_and_get_user()
{
    [ "${execute_user}"x = ""x ] && execute_user="kingbase"
    [ "${super_user}"x = ""x ] && super_user="root"
}

function test_ssh()
{
    local host=$1

    execute_command ${super_user} $host "/bin/true 2>/dev/null"
    if [ $? -eq 0 ]
    then
        return 0
    fi
    execute_command ${super_user} $host "/usr/bin/true 2>/dev/null"
    if [ $? -eq 0 ]
    then
        return 0
    fi
    return 1
}

function execute_command()
{
    local user=$1
    local host=$2
    local command=$3

    ssh -q -o Batchmode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=$connection_timeout -l ${user} -T $host "${command}"
    [ $? -ne 0 ] && return 1

    return 0
}


function check_net()
{
    # ssh 检查每个节点是否能够连接
    echo "[RUNNING] check the ssh can be reached ..."
    for((i=1;i<=5;i++))
    do
        for ip in ${all_ip[@]}
        do
            test_ssh $ip
            if [ $? -ne 0 ]
            then
                should_exit=1
                echo "[RUNNING] can not ssh to \"${ip}\", please check your configuration item of \"all_ip\"."
                break
            else
                echo "[RUNNING] success ssh to the target \"${ip}\" ..... OK"
            fi
        done
        [ $should_exit -eq 0 ] && break
        echo "[RUNNING] failed to check ssh ($i / 5)"
        [ $i -eq 5 ] && break
        echo "[RUNNING] sleep 5 seconds and check again ..."
        should_exit=0
        sleep 5
    done
    [ $should_exit -eq 1 ] && exit 0
}

function cp_conf() {
    local src_ip=$1
    local dst_ip=$2
    execute_command ${execute_user} ${dst_ip} "test ! -f $repmgr_conf && mkdir -p ${install_dir}/etc && touch $repmgr_conf"
    sleep 5
    echo "[INSTALL] cat the $repmgr_conf from $src_ip to \"${dst_ip}\"....."
    local need_exit=1
    for((i=1;i<=100;i++))
    do
        execute_command ${execute_user} ${src_ip} "cat $repmgr_conf"> $repmgr_conf
        if [ $? -ne 0 ]
        then
            echo "[INSTALL] cat the $repmgr_conf from $src_ip to \"${dst_ip}\"..... try again"
            continue
        else
            need_exit=0
            break
        fi
    done
    [ $need_exit -ne 0 ] && exit 1
    need_exit=1
    echo "[INSTALL] cat the $repmgr_conf from $src_ip to \"${dst_ip}\"..... ok"
    echo "[INSTALL] cat the ~/.encpwd from $src_ip to \"${dst_ip}\"....."

    for((i=1;i<=100;i++))
    do
        execute_command ${execute_user} ${src_ip} "cat ~/.encpwd" > ${install_dir}/etc/.encpwd && chown -R ${execute_user}:${execute_user}  ${install_dir}/etc/.encpwd
        if [ $? -ne 0 ]
        then
            echo "[INSTALL] cat the $repmgr_conf from $src_ip to \"${dst_ip}\"..... try again"
            continue
        else
            need_exit=0
            break
        fi
    done
    [ $need_exit -ne 0 ] && exit 1
    need_exit=1
    for((i=1;i<=100;i++))
    do
        execute_command ${execute_user} ${dst_ip} "cp ${install_dir}/etc/.encpwd ~/.encpwd"
        if [ $? -ne 0 ]
        then
            echo "[INSTALL] cp the ~/.encpwd from ${install_dir}/etc/.encpwd to ~/.encpwd  fail..... try again"
            continue
        else
            need_exit=0
            break
        fi
    done
    [ $need_exit -ne 0 ] && exit 1
    echo "[INSTALL] cat the ~/.encpwd from $src_ip to \"${dst_ip}\"..... ok"
    echo "[INSTALL]  chmod 600 the ~/.encpwd on ${dst_ip}....."
    execute_command ${execute_user} ${dst_ip} "chmod 600 ~/.encpwd"
    if [ $? -ne 0 ]
    then
       echo "[INSTALL] success to chmod 600 the ~/.encpwd on ${dst_ip}.....fail"
       sleep 3
       exit 1
    fi
    echo "[INSTALL] success to chmod 600 the ~/.encpwd on ${dst_ip}..... ok"

}
function getRelname()
{
    local relname=`echo ${primary_host} |awk -F "-"  '{print $1}'`
    echo "$relname"
}
function get_and_update_conninfo()
{
    local primary_host=$1
    local expand_ip=$2
    local conninfo=`execute_command ${execute_user} $primary_host "${sys_bindir}/repmgr cluster show  | grep -v \"Connection string\" |grep $primary_host |awk -F \"|\" '{for(i=1;i<=NF;i++){print \\$i}}'|grep "host=" |tail -n 1| awk '\\$1=\\$1'  |sed \"s/host=\([0-9.:a-zA-Z-]*\) /host=${expand_ip} /g\""`
    echo "$conninfo"
}

function get_max_node_id()
{
    local primary_host=$1
    local max_node_id=`execute_command ${execute_user} ${primary_host} "${sys_bindir}/ksql \"dbname=esrep user=${db_user} port=${db_port} application_name=internal_rwcmgr\" -Atqc \"select node_id from repmgr.nodes order by node_id desc limit 1;\""`
    echo "$max_node_id"
}

function get_current_node_id()
{
    local ip=$1
    local cur_node_id=`execute_command ${execute_user} ${ip} "${sys_bindir}/ksql \"dbname=esrep user=${db_user} port=${db_port} application_name=internal_rwcmgr\" -Atqc \"select node_id from repmgr.nodes where conninfo like '%host=${ip} %';\" "`
    echo "$cur_node_id"
}

function change_repmgr_conf() {
    local primary_ip=$1
    local expand_ip=$2
    local node_id=$3
    cp_conf "$primary_ip" "$expand_ip"
    set_or_update_parm "$expand_ip" ${execute_user} "$repmgr_conf" "node_id='$node_id'"
    set_or_update_parm "$expand_ip" ${execute_user} "$repmgr_conf" "node_name='node$node_id'"
    local conninfo=$(get_and_update_conninfo $primary_ip $expand_ip)
    set_or_update_parm "$expand_ip" ${execute_user} "$repmgr_conf" "conninfo='$conninfo'"
}
function copy_soft_ware()
{
    for ip in ${all_ip[@]}
    do
        copy_soft_ware_local $ip
    done
}

function copy_soft_ware_local()
{
    if [ "${install_dir}"x != "${soft_dir}"x ]
    then
        # 设置安装目录属主 /var/lib/data
        echo "[RUNNING] changing ownership of ${install_dir} on $ip ..."

        execute_command ${super_user} $ip "chown -R ${execute_user}:${execute_user} ${install_dir} || chmod 777 ${install_dir}"
        if [ $? -ne 0 ]
        then
            # don't need to exit
            echo "[RUNNING] failed to changing ownership of \"${install_dir}\" on $ip ."
        else
            echo "[RUNNING] success to changed ownership (or chmod 777) of \"${install_dir}\" on $ip ..... OK"
        fi

        echo "[RUNNING] changing ownership DONE on $ip"
        # 复制软件目录到安装目录下 /var/lib/data
        echo "[RUNNING] copy the soft dir into ${install_dir} on $ip ..."
        execute_command ${execute_user} $ip "cp -r ${soft_dir}/* ${install_dir}/"
        if [ $? -ne 0 ]
        then
            # don't need to exit
            echo "[RUNNING] failed to copy soft dir to \"${install_dir}\" on $ip."
        else
            echo "[RUNNING] success to copy soft dir to \"${install_dir}\" on $ip ..... OK"
        fi

        echo "[RUNNING] copy DONE on $ip"

    fi
}
function check_db_running()
{
    # 检查是否有kingbase数据库正在运行
    echo "[RUNNING] check the db is running ..."
    for ip in ${all_ip[@]}
    do
        local db_running=`execute_command ${super_user} $ip "netstat -apn 2>/dev/null|grep -w \"${db_port}\"|wc -l"`
        if [ $? -ne 0 -o "${db_running}"x != "0"x ]
        then
            should_exit=1
            echo "[RUNNING] the db on \"${ip}:${db_port}\" is running, please stop it first."
        else
            echo "[RUNNING] the db is not running on \"${ip}:${db_port}\" ..... OK"
        fi
    done
    [ $should_exit -eq 1 ] && exit 0
}
function check_master_node_exists() {
        switch_master_timeoutmax=32+12*$REPLICA_COUNT
        for ((i = 1; i <= $switch_master_timeoutmax; i++)); do
                for ip in ${all_ip[@]}; do
                        [ "${ip}"x = "${backup_host}"x ] && continue
                        # 检查是否有kingbase数据库正在运行
                        echo "[RUNNING] check the primary db is running ..."
                        local db_running=`execute_command ${super_user} $ip "netstat -apn 2>/dev/null|grep -w \"${db_port}\"|wc -l"`
                        if [ $? -ne 0 -o "${db_running}"x == "0"x ]
                        then
                                echo "[WARNING] the db on \"${ip}:${db_port}\" is not running"
                continue
                        fi
            # 检查集群是否有主节点运行
                        local is_master=$(execute_command ${execute_user} ${ip} "${sys_bindir}/ksql -d test -U ${db_user} -p ${db_port} -c \"Select pg_is_in_recovery();\"|grep -aE \"f\" |wc -l")
                        if [ $is_master -eq 1 ]
                        then
                            echo "[RUNNING] the primary db running on \"${ip}:${db_port}\"...OK"
                                cur_primary_host=${ip}
                            return 0
                        fi
                done
                sleep 1
        done
    echo "[RUNNING] the primary db not running, exit!"
        exit 1
}
function check_last_node_survive()
{
    local heart_beat_file=$1
    local cur_node_id=""

    cur_node_id=`get_current_node_id ${all_ip[$pod_index]}`
    for ((i = 1; i <= $shrink_max_wait_time; i++)); do
        local date=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "date"`
        local survival_flag=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "survival_flag"`
        local node_id=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "node_id"`
        local ip_addr=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "ip_addr"`
        local cur_date=$(date "+%Y-%m-%d %H:%M:%S")
        local diff_time=$(($(date +%s -d "$cur_date") - $(date +%s -d "$date")));
        if [ $diff_time -lt 2 ]
        then
            echo "check last node survive, time is up to date:$date, last node id:$node_id, current node_id:$cur_node_id ...OK"
            return 0
        else
            execute_command ${execute_user} ${primary_host} "ping -c 3 ${ip_addr} >/dev/null"
            if [ $? -eq 0 ]
            then
                echo "check last node survice, network:$ip_addr can be reached, last node id:$node_id, current node_id:$cur_node_id ...OK"
                return 0
            fi
        fi
        sleep 1
    done
    echo "check last node survice, util shrink_max_wait_time:$shrink_max_wait_time seconds later, network:$ip_addr could not be reached, last node id:$node_id, current node_id:$cur_node_id ...fail"
    return 1
}
function start_repmgrd()
{
    local host=$1
    local is_started=0

    echo "`date +'%Y-%m-%d %H:%M:%S'` begin to start repmgrd on \"[${host}]\"."

    #先根据pid文件确定repmgrd是否已经启动
    local repmgr_pid=`execute_command ${execute_user} $host "cat ${repmgrd_pid_file} 2>/dev/null"`
    if [ $? -eq 0 -a "${repmgr_pid}"x != ""x ]
    then
        is_started=`execute_command ${execute_user} $host "ps hp $repmgr_pid 2>/dev/null |grep -w \"repmgrd\"|wc -l"`
    else
        is_started=`execute_command ${execute_user} $host "ps -ef 2>/dev/null|grep -w \"repmgrd\"| grep -v grep| wc -l"`
    fi

    #ssh执行成功且repmgrd已经启动，直接返回
    if [ $? -eq 0 ] && [ "${is_started}"x != ""x ] && [ ${is_started} -gt 0 ]
    then
        echo "`date +'%Y-%m-%d %H:%M:%S'` repmgrd on \"[${host}]\" already started."
        return 0
    fi

    execute_command ${execute_user} $host "${sys_bindir}/repmgrd -d -v -f ${rep_conf}"
    if [ $? -ne 0 ]
    then
        echo "`date +'%Y-%m-%d %H:%M:%S'` execute to start repmgrd on \"[${host}]\" failed."
        return 1
    fi

    echo "`date +'%Y-%m-%d %H:%M:%S'` repmgrd on \"[${host}]\" start success."

    return 0
}
function stop_repmgrd()
{
    local host=$1
    local is_started=0
    local repmgr_pid=""

    echo "`date +'%Y-%m-%d %H:%M:%S'` begin to stop repmgrd on \"[${host}]\"."

    #先根据pid文件确定repmgrd是否已经启动
    repmgr_pid=`execute_command ${execute_user} $host "cat ${repmgrd_pid_file} 2>/dev/null"`
    if [ $? -eq 0 -a "${repmgr_pid}"x != ""x ]
    then
        is_started=`execute_command ${execute_user} $host "ps hp $repmgr_pid 2>/dev/null |grep -w \"repmgrd\"|wc -l"`
    else
        is_started=`execute_command ${execute_user} $host "ps -ef 2>/dev/null|grep -w \"repmgrd\"| grep -v grep| wc -l"`
        repmgr_pid=""
    fi

    #ssh执行成功且repmgrd已经停止，直接返回
    if [ $? -eq 0 ] && [ "${is_started}"x != ""x ] && [ ${is_started} -eq 0 ]
    then
        echo "`date +'%Y-%m-%d %H:%M:%S'` repmgrd on \"[${host}]\" already stopped."
        return 0
    fi

    if [ "${repmgr_pid}"x != ""x ]
    then
        execute_command ${execute_user} $host "kill -9 ${repmgr_pid} 2>/dev/null"
    else
        execute_command ${execute_user} $host "ps -ef 2>/dev/null|grep -w \"repmgrd\"| grep -v grep| xargs kill -9 2>/dev/null"
    fi

    if [ $? -ne 0 ]
    then
        echo "`date +'%Y-%m-%d %H:%M:%S'` execute to stop repmgrd on \"[${host}]\" failed."
        return 1
    fi

    echo "`date +'%Y-%m-%d %H:%M:%S'` repmgrd on \"[${host}]\" stop success."

    return 0
}
function start_node()
{
    local ip=$1
    echo "start up the standby db on \"${ip}\" ..."
    echo "${sys_bindir}/sys_ctl -w -t 60 -l ${install_dir}/logfile -D ${data_directory} start"
    execute_command ${execute_user} ${ip} "${sys_bindir}/sys_ctl -w -t 60 -l ${install_dir}/logfile -D ${data_directory} start"
    [ $? -ne 0 ] && exit 1
    echo "start up the standby db on \"${ip}\" ... OK"
    execute_command ${execute_user} ${cur_primary_host} "${install_dir}/bin/repmgr cluster show"
    echo "register the standby on \"${ip}\" ..."
    execute_command ${execute_user} ${ip} "${sys_bindir}/repmgr standby register -F"
    [ $? -ne 0 ] && exit 1
    echo "[EXPAND] register the standby on \"${ip}\" ... OK"
    # 启动扩容节点
    echo "[INSTALL] start up the node ..."
    execute_command ${execute_user} ${ip} "${sys_bindir}/sys_monitor.sh startlocal"
    [ $? -ne 0 ] && exit 1
    echo "[INSTALL] start up the node ... OK"
    execute_command ${execute_user} ${ip} "${install_dir}/bin/repmgr cluster show"
}
function expand_data_node() {

    copy_soft_ware_local $init_host
    check_master_node_exists
    [ $? -ne 0 ] && exit 1
    echo "[EXPAND] the current primary_host is :${cur_primary_host} "
    if [ "$cur_primary_host"x != ""x ]; then
        local  node_id=$((`get_max_node_id $cur_primary_host` + 1))
        echo "[EXPAND] current expand node id :$node_id"
        echo "change repmgr conf on \"${init_host}\" ..."
        change_repmgr_conf "${cur_primary_host}" "${init_host}" "$node_id"
        echo "change repmgr conf on \"${init_host}\" ..."

        config_encpwd_local "${init_host}"
        echo "clone the standby on \"${init_host}\" ..."
        echo "${sys_bindir}/repmgr -h ${cur_primary_host} -U esrep -d esrep -p ${db_port} standby clone"
        execute_command ${execute_user} ${init_host} "${sys_bindir}/repmgr -h ${cur_primary_host} -U esrep -d esrep -p ${db_port} standby clone"
        if [ $? -ne 0 ]
        then
            echo "clone the standby on \"${init_host}\" ... fail!"
            sleep 3
            exit 1
        fi
        echo "clone the standby on \"${init_host}\" ... OK"
        config_nodetools_conf_local  "${init_host}"
        start_node "${init_host}"
        scale_rman "addnode" "$cur_primary_host" "$init_host"
        start_kmonitor_local "${init_host}"
        register_kmonitor_local "${init_host}"
    fi
}
function sshstopdb() {
    local primary_ip=$1

    local shrink_ip=$2

    execute_command ${execute_user} $shrink_ip "$sys_bindir/sys_monitor.sh stoplocal 2>/dev/null"
    if [ $? -eq 0 ]
    then
        return 0
    fi
    local db_exsit=$(execute_command ${execute_user} $primary_ip "$sys_bindir/ksql -h $primary_ip -U esrep -d esrep -p $db_port -c \"select * from pg_stat_replication;\" |grep -w \"$shrink_ip\" |wl -c")

    if [ $db_exsit -eq 0 ]; then
        echo " [INFO] stop standby db failed,but But the streaming replication connection of db has been disconnected, which does not affect the delete replication slot operation."
    else
        echo " [ERROR] stop standby db failed,the stream replication connection of db still exists, which affects the host to delete the replication slot and exits with an error."
        exit 1
    fi
}
function drop_standby_node() {
    local primary_ip=$1
    local shrink_ip=$2
    local node_id=$3
    echo "[$(date)] [INFO] ${sys_bindir}/repmgr standby unregister --node-id=$node_id ..."
    execute_command ${execute_user} ${shrink_ip} "$sys_bindir/repmgr standby unregister --node-id=$node_id"
    [ $? -ne 0 ] && return 0
    echo "[$(date)] [INFO] ${sys_bindir}/repmgr standby unregister --node-id=$node_id ...OK"

    sshstopdb $primary_ip $shrink_ip

    execute_command ${execute_user} ${primary_ip} "${install_dir}/bin/repmgr cluster show"

    slot_name="repmgr_slot_$node_id"

    echo "[$(date)] [INFO]  drop replication slot:$slot_name..."
    execute_command ${execute_user} ${primary_ip} "${install_dir}/bin/ksql -h $primary_ip -U esrep -d esrep -p $db_port -c \"select * from pg_drop_replication_slot('$slot_name');\""
    if [ $? -ne 0 ]
    then
        echo "[$(date)] [INFO]  drop replication slot:$slot_name...fail!"
        return 0
    fi
    echo "[$(date)] [INFO]  drop replication slot:$slot_name...OK"
}

function check_last_node_and_shrink()
{
    local max_survival_node_id=0
    local max_node_id=0
    local max_survival_date=""
    #这里检查到data目录不存在  当前是备份节点，或者数据库节点data被删掉了，可以直接判定可以缩容
    test -d ${data_directory}
    if [ $? -ne 0 ]
    then
        echo "check last node survive, there is no data dir in current node  ...OK"
        return 0
    fi
    local cur_node_id=`get_current_node_id ${all_ip[$pod_index]}`
    echo  "[RUNNING] cur_node_id:$cur_node_id"
    if [ "$function_name"x == "shrink"x ]
    then
        echo "[RUNNING] check if the current node is the last one ..."
        sleep 3
        heart_beat_files=$(execute_command ${super_user} $primary_host "ls -l ${install_dir}/etc |awk -F \" \" '{ print \$9 }' |grep heart_beat_node")
        if [ "$heart_beat_files"x != ""x ]
        then
            for heart_beat_file in $heart_beat_files
            do
                local date=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "date"`
                local survival_flag=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "survival_flag"`
                local node_id=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "node_id"`
                local ip_addr=`read_conf_value "$primary_host" "${install_dir}/etc/$heart_beat_file" "ip_addr"`
                echo "[RUNNING]  date:$date survival_flag:$survival_flag node_id:$node_id  ip_addr:$ip_addr  heart_beat_file:$heart_beat_file"
                if [ $max_survival_node_id -lt $node_id -a "$survival_flag"x == "true"x ]
                then
                    max_survival_node_id=$node_id
                    max_survival_date=$date
                fi
                echo "max_node_id:$max_node_id max_survival_node_id:$max_survival_node_id"
                if [ $max_node_id -lt $node_id ]
                then
                    max_node_id=$node_id
                    max_date=$date
                fi
            done
        fi
        echo "[RUNNING] pod_index:$pod_index cur_node_id:$cur_node_id max_node_id:$max_node_id max_survival_node_id:$max_survival_node_id max_survival_date:$max_survival_date max_date:$max_date"
        if [ $cur_node_id -eq $max_node_id ]
        then
            check_master_node_exists
            drop_standby_node "$cur_primary_host" "${all_ip[$pod_index]}" "$cur_node_id"
            scale_rman "delnode" "$cur_primary_host" "${all_ip[$pod_index]}"
            unregister_kmonitor_local "${all_ip[$pod_index]}"
            need_exit=1
            for((i=1;i<=100;i++))
            do
                execute_command ${super_user} $primary_host "rm -rf ${install_dir}/etc/heart_beat_node$max_node_id"
                if [ $? -ne 0 ]
                then
                    echo "[INSTALL] delete ${install_dir}/etc/heart_beat_node$max_node_id fail..... try again"
                    continue
                else
                    execute_command ${super_user} $primary_host "ls -l ${install_dir}/etc/"
                    need_exit=0
                    break
                fi
            done
            [ $need_exit -ne 0 ] && sleep 5 && exit 1
            echo "[INSTALL] delete ${install_dir}/etc/heart_beat_node'$max_node_id' .....OK"
        else
            # 缩容节点是活着的最后一个节点，此时需要考虑，后面节点正在重建pod,需要等待pod重建完成(暂未处理，不处理数据，不存在丢数据风险)
            if [ $cur_node_id -eq $max_survival_node_id ]
            then
                check_last_node_survive "${install_dir}/etc/heart_beat_node$max_survival_node_id"
                if [ $? -eq 0 ]
                then
                   echo "[RUNNING] last node survive, current node_id:$cur_node_id is not last node! no need to execute shrink, exit!"
                   exit 1
                fi
                check_master_node_exists

                drop_standby_node "$cur_primary_host" "${all_ip[$pod_index]}" "$cur_node_id"
                scale_rman "delnode" "$cur_primary_host" "${all_ip[$pod_index]}"
                unregister_kmonitor_local "${all_ip[$pod_index]}"
                need_exit=1
                for((i=1;i<=100;i++))
                do
                    execute_command ${super_user} $primary_host "rm -rf ${install_dir}/etc/heart_beat_node$max_node_id"
                    if [ $? -ne 0 ]
                    then
                        echo "[INSTALL] delete ${install_dir}/etc/heart_beat_node'$max_node_id' fail..... try again"
                        continue
                    else
                        execute_command ${super_user} $primary_host "ls -l ${install_dir}/etc/"
                        need_exit=0
                        break
                    fi
                done
                [ $need_exit -ne 0 ] && sleep 5 && exit 1
                echo "[INSTALL] delete ${install_dir}/etc/heart_beat_node'$max_node_id' .....OK"
            else
                echo "[RUNNING] current node:$cur_node_id restart! should do nothing, exit!"
                exit 1
            fi
        fi
    fi
}
function choose_exec_mode() {
    if [ "$function_name"x == "shrink"x ]
    then
        echo "function_name:$function_name."
        check_last_node_and_shrink
        exit 0
    fi
    # 检查是否有kingbase data目录
    echo "[RUNNING] check if the data directory exists ..."
    all_node_num=${#all_ip[@]}
    data_directory_count=0
    for ip in ${all_ip[@]}; do
        [ "${ip}"x = "${backup_host}"x ] && continue
        execute_command ${execute_user} $ip "test -d ${data_directory}"
        if [ $? -eq 0 ]
        then
            let data_directory_count++
            echo "[RUNNING] the data directory on \"${ip}\" exists. ${data_directory_count}/${all_node_num}."
        else
            echo "[RUNNING] the data directory on \"${ip}\" not exists.${data_directory_count}/${all_node_num}."
        fi
    done
    # 确定脚本执行模式：初始化或者扩容
    if [ ${data_directory_count} -eq 0 ]; then
        echo "[RUNNING] ALL db nodes haven't initialized yet, execute initializing."
        check_db_running
        initializing
    else
        # 拉起本地repmrd
        echo "[RUNNING] ALL db nodes hava been initialized, check if data_directory:${data_directory} exist on localhost."
        test -d ${data_directory}
        if [ $? -eq 0 ]
        then
            echo "[RUNNING] ALL db nodes hava been initialized, data_directory:${data_directory} exist on \" ${ip}\", start repmgrd..."
            #心跳以及kbha的定时任务
            start_repmgrd localhost
            [ $? -eq 0 ] && echo "[RUNNING] ALL db nodes hava been initialized, data_directory:${data_directory} exist on \" ${ip}\", start repmgrd OK"
            exit 0
        fi
        # 扩容节点
        echo "[RUNNING] ALL db nodes hava been initialized, execute expand."
        expand_data_node

        #心跳文件分开
        echo "[RUNNING] config envfile local..."
        config_envfile_local "$init_host"
        echo "[RUNNING] config envfile local...OK"
        echo "[RUNNING] config hearbeat local..."
        config_auto_heartbeat_local "$init_host"
        echo "[RUNNING] config hearbeat local...OK"
        exit 0
    fi
}
function check_install_dir()
{
    # 检查安装目录是否存在, 检查目录层级，应该是 ${install_dir}/bin
    echo "[RUNNING] check the install dir is already exist ..."
    for ip in ${all_ip[@]}
    do
        execute_command ${execute_user} $ip "test ! -d ${install_dir}/bin"
        if [ $? -eq 0 ]
        then
            should_exit=1
            echo "[RUNNING] the target \"${install_dir}/bin\" on \"${ip}\" is not exist, please install it first."
        else
            echo "[RUNNING] the install dir is already exist on \"${ip}\" ..... OK"
        fi
    done
    [ $should_exit -eq 1 ] && exit 0
}

function read_conf_value() {
    local ip=$1
    local conf_path=$2
    local conf_key=$3
    execute_command ${execute_user} ${ip} "test -f $conf_path"
    [ $? -ne 0 ] && exit 1
    local value=$(execute_command ${execute_user} ${ip} "cat $conf_path |grep -aEw \"$conf_key\" |tail -n 1|awk -F '=' '{print \$2}' | tr -d [\'] ")
    echo $value
}

function read_conf_value_local() {
    local conf_path=$1
    local conf_key=$2
    test -f "$conf_path"
    [ $? -ne 0 ] && exit 1
    local value=`cat $conf_path |grep -aEw "$conf_key" |tail -n 1|awk -F '=' '{print $2}' | tr -d [\']`
    echo $value
}
function set_or_update_parm() {
    local ip=$1
    local execute_user=$2
    local conf_name=$3
    local parameter=$4
    local delimiter=""

    parameter_name=$(echo $parameter | awk -F "=" '{print $1}')
    parameter_values=$(echo $parameter | awk -F "=" '{print $2}')
    echo " [INFO] $parameter_name=$parameter_values"
    if [ "$parameter_name"x != ""x -a "$parameter_values"x != ""x -a "$parameter_values"x != "''"x ]; then
        delimiter="="
    elif [ "$parameter_values"x = ""x ] && [ "$(echo $parameter | grep = | wc -l)"x = "0"x ]; then
        local value1=""
        local value2=""

        value1=execute_command ${execute_user} ${ip} "echo $parameter | awk -F " " '{print \\$1}' 2>/dev/null"
        value2=execute_command ${execute_user} ${ip} "echo $parameter | awk -F " " '{print \\$2}' 2>/dev/null"
        if [ "$value1"x != ""x -a "$value2"x != ""x -a "$value2"x != "''"x ]; then
            parameter_name="${value1}"
            parameter_values="${value2}"
            delimiter="[ ]"
            echo " [INFO] $parameter_name=$parameter_values"
        fi
    fi
    if [ "${delimiter}"x != ""x ]; then
        para_exist=$(execute_command ${execute_user} ${ip} "grep -wRn $parameter_name $conf_name |wc -l")
        if [ $para_exist -eq 0 ]; then
            echo " [INFO] \"$parameter\" >> $conf_name"
            execute_command ${execute_user} ${ip} "echo \"$parameter\" >> $conf_name"
        else
            if [ "$function_name"x == "config_auto_heartbeat"x ]
            then
                execute_command ${execute_user} ${ip} "sed -i \"/^[# ]*${parameter_name}[ ]*${delimiter}/c\"${parameter}\"\" $conf_name"
            else
                execute_command ${execute_user} ${ip} "sed -i \"/^[# ]*${parameter_name}[ ]*${delimiter}/c${parameter}\" $conf_name"
            fi
        fi
    fi
}

function check_and_change_arping()
{
     # 配置了VIP，修改ip、arping的权限
    if [ "${virtual_ip}"x != ""x ]
    then
        echo "[RUNNING] chmod u+s for \"${ipaddr_path}\" and \"${arping_path}\""
        for ip in ${all_ip[@]}
        do
            [ "${ip}"x = "${backup_host}"x ] && continue
            execute_command ${super_user} $ip "chmod u+s ${ipaddr_path}/ip"
            if [ $? -ne 0 ]
            then
                should_exit=1
                echo "[RUNNING] can not execute \"chmod u+s ${ipaddr_path}/ip\" on \"${ip}\"."
                break
            else
                echo "[RUNNING] chmod u+s ${ipaddr_path}/ip on \"${ip}\" ..... OK"
            fi

            execute_command ${super_user} $ip "chmod u+s ${arping_path}/arping"
            if [ $? -ne 0 ]
            then
                should_exit=1
                echo "[RUNNING] can not execute \"chmod u+s ${arping_path}/arping\" on \"${ip}\"."
                break
            else
                echo "[RUNNING] chmod u+s ${arping_path}/arping on \"${ip}\" ..... OK"
            fi
        done
        [ $should_exit -eq 1 ] && exit 0
    fi
}

function mkdir_archive()
{
    # 创建归档目录archive, 配置文件目录etc、repmgr.conf配置文件
    echo "[INSTALL] create the dir \"${install_dir}/etc\" on all host"
    for ip in ${all_ip[@]}
    do
        execute_command ${execute_user} ${ip} "test ! -d ${install_dir}/etc && mkdir ${install_dir}/etc"
        execute_command ${execute_user} ${ip} "test ! -d ${install_dir}/archive && mkdir ${install_dir}/archive"
        execute_command ${execute_user} ${ip} "test ! -f ${install_dir}/etc/repmgr.conf && touch ${install_dir}/etc/repmgr.conf"
    done
}
function init_db()
{
    # 初始化数据库
    echo "[INSTALL] begin to init the database on \"${primary_host}\" ..."
    if [ "${db_auth}"x = "trust"x ]
    then
        execute_command ${execute_user} ${primary_host} "${sys_bindir}/initdb -D ${data_directory} -U $db_user -A ${db_auth} ${initdb_options}"
    else
        execute_command ${execute_user} ${primary_host} "${sys_bindir}/initdb -D ${data_directory} -U $db_user -A ${db_auth} -x '$db_password' ${initdb_options}"
    fi
    [ $? -ne 0 ] && exit 0
    echo "[INSTALL] end to init the database on \"${primary_host}\" ... OK"
}
function config_kingbase_conf()
{
    # 配置数据库配置文件kingbase.conf
    echo "[INSTALL] wirte the kingbase.conf on \"${primary_host}\" ..."
    execute_command ${execute_user} ${primary_host} "sed -i -e \"/^shared_preload_libraries[ ]*=[ ]*'*'/s/'/'repmgr,/\" ${data_directory}/kingbase.conf"
    execute_command ${execute_user} ${primary_host} "echo -e \"\ninclude_if_exists = 'es_rep.conf'\" >> ${data_directory}/kingbase.conf"
    execute_command ${execute_user} ${primary_host} "test ! -f ${data_directory}/es_rep.conf && touch ${data_directory}/es_rep.conf"
    execute_command ${execute_user} ${primary_host} "cat >> ${data_directory}/es_rep.conf <<_ESEOF
listen_addresses = '*'
port = ${db_port}
full_page_writes = on
wal_log_hints = on
max_wal_senders = 32
wal_keep_segments = 512
max_replication_slots = 32
hot_standby = on
hot_standby_feedback = on
logging_collector = on
archive_mode = on
archive_command = 'test ! -f ${install_dir}/archive/%f && cp %p ${install_dir}/archive/%f'
_ESEOF"
    if [ "${DB_PARAMS}"x != ""x ]
    then
        execute_command ${execute_user} ${primary_host} "echo \"${DB_PARAMS}\" >> ${data_directory}/es_rep.conf"
    fi
    echo "[INSTALL] wirte the kingbase.conf on \"${primary_host}\" ... OK"
}
function config_sys_hba_conf()
{
    # 配置数据库身份认证文件sys_hba.conf
    echo "[INSTALL] wirte the sys_hba.conf on \"${primary_host}\" ..."
    execute_command ${execute_user} ${primary_host} "sed -i -e \"s/\(^local\(.*\)\)\(md5$\)/\1trust/g\" ${data_directory}/sys_hba.conf"
    execute_command ${execute_user} ${primary_host} "cat >> ${data_directory}/sys_hba.conf <<_HBAEOF
host    replication     all             0.0.0.0/0               ${db_auth}
host    all             all             0.0.0.0/0               ${db_auth}
_HBAEOF"
    echo "[INSTALL] wirte the sys_hba.conf on \"${primary_host}\" ... OK"
}
function config_repmgr_conf()
{
    # 配置repmgr.conf
    local id_count=1
    local has_host=`echo ${conninfo} 2>/dev/null| grep -w "host="|wc -l`
    echo "write the repmgr.conf on every host"
    for ip in ${all_ip[@]}
    do
        local node_conninfo=""
        [ "${ip}"x = "${backup_host}"x ] && continue

        if [ $has_host -eq 0 ]
        then
            node_conninfo="host=$ip ${conninfo}"
        else
            node_conninfo="${conninfo} host=$ip"
        fi

        echo "[INSTALL] write the repmgr.conf on \"${ip}\" ..."

        execute_command ${execute_user} $ip "cat >> ${install_dir}/etc/repmgr.conf <<_REPEOF
node_id=$id_count
node_name='node${id_count}'
conninfo='${node_conninfo}'

data_directory='${data_directory}'
log_file='${log_file}'
kbha_log_file='${kbha_log_file}'
sys_bindir='${sys_bindir}'
ssh_options='-q -o ConnectTimeout=10 -o StrictHostKeyChecking=no'

trusted_servers='${trusted_servers}'
repmgrd_pid_file='${repmgrd_pid_file}'

synchronous='quorum'
failover='automatic'
recovery='automatic'
auto_cluster_recovery_level='1'
reconnect_attempts=${reconnect_attempts}
reconnect_interval=${reconnect_interval}
use_scmd='off'

promote_command='${sys_bindir}/repmgr standby promote -f ${install_dir}/etc/repmgr.conf'
follow_command='${sys_bindir}/repmgr standby follow -f ${install_dir}/etc/repmgr.conf -W --upstream-node-id=%n'
_REPEOF"

        if [ "${virtual_ip}"x != ""x ]
        then
            execute_command ${execute_user} $ip "cat >> ${install_dir}/etc/repmgr.conf <<_REPEOF
virtual_ip='${virtual_ip}'
ipaddr_path='${ipaddr_path}'
arping_path='${arping_path}'
net_device='${net_device}'
_REPEOF"
        fi
        echo "[INSTALL] write the repmgr.conf on \"${ip}\" ... OK"
        let id_count++
    done
}
function config_nodetools_conf()
{
    # config all_nodes_tools.conf file
    db_base64_pass=`echo "$db_password" | base64 -w 0`
    for ip in ${all_ip[@]}
    do
        [ "${ip}"x = "${backup_host}"x ] && continue
        config_nodetools_conf_local ${ip}
    done
}
function config_nodetools_conf_local()
{
    local ip=$1
    execute_command ${execute_user} $ip "echo \"db_u=$db_user\" > ${install_dir}/etc/all_nodes_tools.conf"
    execute_command ${execute_user} $ip "echo \"db_password=$db_base64_pass\" >> ${install_dir}/etc/all_nodes_tools.conf"
    execute_command ${execute_user} $ip "echo \"db_port=$db_port\" >> ${install_dir}/etc/all_nodes_tools.conf"
    execute_command ${execute_user} $ip "echo \"db_name=test\" >> ${install_dir}/etc/all_nodes_tools.conf"
}
function config_envfile()
{
    for ip in ${all_ip[@]}
    do
        config_envfile_local $ip
    done
}
function config_envfile_local()
{
    local ip=$1
    # 保存环境变量到配置文件，供定时任务使用
    echo "[CONFIG] config ${env_file} on $ip ..."
    execute_command "$execute_user" "$ip" "test ! -f  ${env_file} && mkdir -p ${install_dir}/etc && touch ${env_file} "
    execute_command "$execute_user" "$ip" "env |grep -v SSH_ >> ${env_file}"
    # 同样的env_file复制到/home/kingbase/cluster/etc下
    execute_command "$execute_user" "$ip" "mkdir -p /home/kingbase/cluster/etc && cp  ${env_file}  /home/kingbase/cluster/etc"
    set_or_update_parm "$ip" ${execute_user} "${env_file}" "ALL_NODE_IP=$ALL_NODE_IP"
    set_or_update_parm "$ip" ${execute_user} "${env_file}" "REPLICA_COUNT=$REPLICA_COUNT"
    set_or_update_parm "$ip" ${execute_user} "${env_file}" "TRUST_IP=$TRUST_IP"
    set_or_update_parm "$ip" ${execute_user} "${env_file}" "install_dir=${install_dir}"
    [ $? -eq 0 ] && echo "[CONFIG] config ${env_file} on $ip ...OK"
}
function config_encpwd()
{
    if [ "${db_auth}"x != "trust"x ]
    then
        local pass_file=""
        if [ "${execute_user}"x = "root"x ]
        then
            pass_file="/root/.encpwd"
        else
            pass_file="/home/${execute_user}/.encpwd"
        fi
        # 后续需要连接数据库，配置免密配置文件 ~/.encpwd
        for ip in ${all_ip[@]}
        do
            echo "[INSTALL] wirte the ~/.encpwd on \"${ip}\" ..."
            execute_command ${execute_user} ${ip} "${sys_bindir}/sys_encpwd -H \\* -P \\* -D \\* -U ${db_user} -W '${db_password}'"
            execute_command ${execute_user} ${ip} "${sys_bindir}/sys_encpwd -H \\* -P \\* -D \\* -U esrep -W '${esrep_password}'"
            echo "[INSTALL] wirte the ~/.encpwd on \"${ip}\" ... OK"

            echo "[INSTALL] copy ~/.encpwd to ${install_dir}/etc on \"${ip}\" ..."
            execute_command ${execute_user} ${ip} "test -f ${pass_file} && cp ${pass_file} ${install_dir}/etc"
            [ $? - ne 0 ] && exit 0
            echo "[INSTALL] copy ~/.encpwd to ${install_dir}/etc on \"${ip}\" ... OK"

            echo "[INSTALL] chmod 600 ${install_dir}/etc/.encpwd on \"${ip}\" ..."
            execute_command ${execute_user} ${ip} "chmod 600 ${install_dir}/etc/.encpwd"
            [ $? - ne 0 ] && exit 0
            echo "[INSTALL] chmod 600 ${install_dir}/etc/.encpwd on \"${ip}\" ...OK"
        done
    fi
}
function config_encpwd_local()
{
    local ip=$1
    if [ "${db_auth}"x != "trust"x ]
    then
        local pass_file=""
        if [ "${execute_user}"x = "root"x ]
        then
            pass_file="/root/.encpwd"
        else
            pass_file="/home/${execute_user}/.encpwd"
        fi
        # 后续需要连接数据库，配置免密配置文件 ~/.encpwd
        echo "[INSTALL] wirte the ~/.encpwd on \"${ip}\" ..."
        execute_command ${execute_user} ${ip} "${sys_bindir}/sys_encpwd -H \\* -P \\* -D \\* -U ${db_user} -W '${db_password}'"
        execute_command ${execute_user} ${ip} "${sys_bindir}/sys_encpwd -H \\* -P \\* -D \\* -U esrep -W '${esrep_password}'"
        echo "[INSTALL] wirte the ~/.encpwd on \"${ip}\" ... OK"
        echo "[INSTALL] copy ~/.encpwd to ${install_dir}/etc on \"${ip}\" ..."
        execute_command ${execute_user} ${ip} "test -f ${pass_file} && cp ${pass_file} ${install_dir}/etc"
        [ $? - ne 0 ] && exit 0
        echo "[INSTALL] copy ~/.encpwd to ${install_dir}/etc on \"${ip}\" ... OK"
        execute_command ${execute_user} ${ip} "chmod 600 ~/.encpwd"
        if [ $? -ne 0 ]
        then
            echo "[INSTALL] success to chmod 600 the ~/.encpwd on ${dst_ip}.....fail"
            sleep 3
            exit 1
        fi
        echo "[INSTALL] success to chmod 600 the ~/.encpwd on ${dst_ip}..... ok"
    fi
}
function start_db()
{
    local ip=$1
    # 启动数据库
    echo "[INSTALL] start up the database on \"${ip}\" ..."
    echo "[INSTALL] ${sys_bindir}/sys_ctl -w -t 60 -l ${install_dir}/logfile -D ${data_directory} start"
    execute_command ${execute_user} ${ip} "${sys_bindir}/sys_ctl -w -t 60 -l ${install_dir}/logfile -D ${data_directory} start"
    [ $? -ne 0 ] && exit 0
    echo "[INSTALL] start up the database on \"${ip}\" ... OK"

    # 创建repmgr需要的database,user，并注册主节点
    echo "[INSTALL] create the database \"esrep\" and user \"esrep\" for repmgr ..."
    execute_command ${execute_user} ${ip} "${sys_bindir}/ksql -d test -U ${db_user} -p ${db_port} -c \"create database esrep;\""
    execute_command ${execute_user} ${ip} "${sys_bindir}/ksql -d test -U ${db_user} -p ${db_port} -c \"create user esrep with superuser password '${esrep_password}';\""
    echo "[INSTALL] create the database \"esrep\" and user \"esrep\" for repmgr ... OK"
    echo "[INSTALL] register the primary on \"${ip}\" ..."
    execute_command ${execute_user} ${ip} "${sys_bindir}/repmgr primary register"
    [ $? -ne 0 ] && exit 0
    echo "[INSTALL] register the primary on \"${ip}\" ... OK"
}

function scale_rman()
{
    local method=$1
    local primary_host=$2
    local scale_ip=$3
    local rman_node_id_max=0
    local should_exit=1
    for m in {"addnode","delnode"}
    do
        [ "$method"x == "$m"x ] && should_exit=0
    done
    [ $should_exit -eq 1 ] && echo "[ERROR] method:$method can not be just set as addnode or delnode!" && exit 1
    echo "[RUNNING] query archive command at ${primary_host} ..."

    local query_result=`execute_command ${execute_user} ${primary_host} "${sys_bindir}/ksql \"dbname=esrep user=${db_user} port=${db_port} password='${db_password}' application_name=internal_rwcmgr\" -c \"show archive_command;\""`
    echo "[RUNNING] query_result: ${query_result}"
    local rman_command_exit=`echo $query_result |grep sys_rman|wc -l`
    local repo_list=""
    if [ "$rman_command_exit"x == "1"x ]
    then
       local rman_conf_path=`echo $query_result |awk -F " " '{for(i=1;i<=NF;i++){print \$i}}'|grep "sys_rman.conf"`
       execute_command ${super_user} $primary_host "test -f ${rman_conf_path}"
       [ $? -ne 0 ] && echo "[WARNING] there is no file \"${rman_conf_path}\" found on $primary_host return" && return 0
       echo "[RUNNING] query archive command at ${primary_host} ...OK"
    else
       echo "[RUNNING] current cluster not config sys_rman,return."
       return 0
    fi

    for ip_in_center in ${all_ip[@]}
    do

        [ "$scale_ip"x == "$ip_in_center"x ] && continue
        execute_command ${super_user} $ip_in_center "test -f ${rman_conf_path}"
        [ $? -ne 0 ] && echo "[WARNING] there is no file \"${rman_conf_path}\" found on $ip_in_center continue" && continue
        echo "[WARNING] there is  \"${rman_conf_path}\" found on $ip_in_center "
        local is_kb_node=`execute_command ${execute_user} ${ip_in_center} "cat ${rman_conf_path} |grep repo1-host=|wc -l"`
        if [ "$is_kb_node"x == "1"x ]
        then
            # record ip of repo node
            repo_list=`execute_command ${execute_user} ${ip_in_center} "cat ${rman_conf_path} |grep -aE \"repo.*-host=\"  |awk -F \"=\" '{print \\$2}'"`
            echo "[RUNNING] get repo_list: ${repo_list} on ${ip_in_center}"
            [ "$repo_list"x == ""x ] && continue
            echo "[RUNNNING] fetch repo node ip:${repo_list} from ${rman_conf_path} at ${ip_in_center}. ...OK"
            if [ "$method"x == "addnode"x ]
            then
                # cp sys_rman.conf to expand_node
                execute_command ${execute_user} ${scale_ip} "test ! -f `dirname ${rman_conf_path}` && mkdir -p  `dirname ${rman_conf_path}`"
                echo "[RUNNNING] cp  ${rman_conf_path}  from ${ip_in_center} to ${scale_ip} at ${ip_in_center}..."

                if [ $deploy_by_sshd -eq 0 ]
                then
                    execute_command ${execute_user} ${scale_ip} "$sys_bindir/sys_securecmd -p $scmd_port -o StrictHostKeyChecking=no -o ConnectTimeout=${connection_timeout} -l ${execute_user} -T $ip_in_center \"cat $rman_conf_path \" > $rman_conf_path"
                    [ $? -ne 0 ] && continue
                    echo "[RUNNNING] success to copy the  ${rman_conf_path}  from ${ip_in_center} to ${scale_ip} at ${ip_in_center}...OK"
                else
                    execute_command ${execute_user} ${ip_in_center} "scp -q -P 22 -o StrictHostKeyChecking=no -r ${rman_conf_path} ${execute_user}@[${scale_ip}]:${rman_conf_path}"
                    [ $? -ne 0 ] && continue
                    echo "[RUNNNING] success to copy the  ${rman_conf_path}  from ${ip_in_center} to ${scale_ip} at ${ip_in_center}...OK"
                fi
            fi
            break
        fi
    done

    if [ "$method"x == "delnode"x ]
    then
        local shrink_is_kb_node=`execute_command ${execute_user} ${scale_ip} "cat ${rman_conf_path} |grep repo1-host=|wc -l"`
        if [ "$shrink_is_kb_node"x == "1"x ]
        then
            echo "[RUNNNING] delete  `dirname ${rman_conf_path}`  at ${scale_ip}..."
            execute_command ${execute_user} ${scale_ip} "rm -rf  `dirname ${rman_conf_path}`&& test ! -f  ${rman_conf_path}"
            [ $? -ne 0 ] && exit 1
            echo "[RUNNNING] delete  `dirname ${rman_conf_path}`  at ${scale_ip}...OK"
         fi
    fi
    echo "[RUNNING] repo list: ${repo_list}"
    for repo_node_ip in ${repo_list}
    do
        if [ "$method"x == "addnode"x ]
        then
            echo "[RUNNNING] expand_ip exist in ${rman_conf_path} at ${repo_node_ip}..."
            local scale_ip_exist=`execute_command ${execute_user} ${repo_node_ip} "cat ${rman_conf_path} |grep -awE $scale_ip|wc -l"`
            if [ "$scale_ip_exist"x != "0"x ]
            then
                echo "[RUNNNING] expand_ip already exist in ${rman_conf_path} at ${repo_node_ip},continue"
                continue
            else
                echo "[RUNNNING] expand_ip not exist in ${rman_conf_path} at ${repo_node_ip}...OK"
            fi

            # fetch new id
            local repo_ids=`execute_command ${execute_user} ${repo_node_ip} "cat ${rman_conf_path} |grep -aE \"^kb.*-path=\"|awk -F \"-\" '{ print \\$1 }'|tr -d \"A-Za-z\""`
            for id in $repo_ids
            do
                [ $rman_node_id_max -lt $id ] && rman_node_id_max=$id
            done
            let rman_node_id_max++
            # add new node
            echo "[RUNNNING] add repo node ip:${scale_ip} rman node_id:${rman_node_id_max} in ${rman_conf_path} at ${repo_node_ip}. ..."
            execute_command ${execute_user} $repo_node_ip "sed -e \"/^\[global\]/ikb${rman_node_id_max}-path=${data_directory}\nkb${rman_node_id_max}-port=${db_port}\nkb${rman_node_id_max}-user=esrep\nkb${rman_node_id_max}-host=${scale_ip}\nkb${rman_node_id_max}-host-user=${execute_user}\" ${rman_conf_path} > ${rman_conf_path}.tmp && cat ${rman_conf_path}.tmp > ${rman_conf_path} && /bin/rm -rf ${rman_conf_path}.tmp"
            [ $? -ne 0 ] && exit 1
            echo "[RUNNNING] add repo node ip:${scale_ip} rman node_id:${rman_node_id_max} in ${rman_conf_path} at ${repo_node_ip}. ...OK"
        elif [ "$method"x == "delnode"x ]
        then
            local delete_ids=`execute_command ${execute_user} ${repo_node_ip} "cat ${rman_conf_path} |grep -awE \"host=$scale_ip\"|awk -F \"-\" '{ print \\$1 }'|tr -d \"A-Za-z\""`
            for delete_id in $delete_ids
            do
                echo "[RUNNNING] del repo node ip:${scale_ip} rman node_id:${delete_id} in ${rman_conf_path} at ${repo_node_ip}. ..."
                execute_command ${execute_user} ${backup_host} "sed -e \"/kb${delete_id}-/d\" ${rman_conf_path} > ${rman_conf_path}.tmp && cat ${rman_conf_path}.tmp > ${rman_conf_path} && /bin/rm -rf ${rman_conf_path}.tmp"
                [ $? -ne 0 ] && continue
                echo "[RUNNNING] del repo node ip:${scale_ip} rman node_id:${delete_id} in ${rman_conf_path} at ${repo_node_ip}. ...OK"
            done
            # need reset the order of kb id
            local new_id=1
            kbnode_ids=`execute_command ${execute_user} ${repo_node_ip}  "cat ${rman_conf_path} |grep kb.*-host= | awk -F '-host=' '/-host=/ {print  \\$1}' | awk -F '-' '{print \\$1} '| tr -d [A-Za-z-]"`
            for id in $kbnode_ids
            do
                [ "$id"x == "$new_id"x ] && echo "[RUNNNING]  kb_id:$id is same as new id:$new_id at ${repo_node_ip}, continue" && let new_id++ && continue
                echo "[RUNNNING] reset kb_id:$id as new id:$new_id at ${repo_node_ip} ..."
                execute_command ${execute_user} ${repo_node_ip} "sed -e \"s/kb${id}-/kb${new_id}-/g\" ${rman_conf_path}.tmp > ${rman_conf_path}.tmp && cat ${rman_conf_path}.tmp > ${rman_conf_path} && /bin/rm -rf ${rman_conf_path}.tmp"
                [ $? -ne 0 ] && continue
                echo "[RUNNNING] reset kb_id:$id as new id:$new_id at ${repo_node_ip}  ...OK"
                let new_id++
            done
        fi
    done
}

function start_cluster_kmonitor()
{
    [ "${kmonitor_enable}"x != "true"x ] && return
    for ip in ${all_ip[@]}
    do
        start_kmonitor_local $ip
        [ "${ip}"x == "${backup_host}"x ] && continue
        register_kmonitor_local $ip
    done
}
function init_kmonitor_local()
{
    local ip=$1
    local config_path="${install_dir}/kmonitor/kmonitor.properties"

    [ "${kmonitor_enable}"x != "true"x ] && return

    # 初始化
    execute_command ${super_user} ${ip} "${install_dir}/kmonitor/kmonitor.sh init"

    # 注册角色
    echo "[INSTALL]  execute UserInit.sql on \"${ip}\" ..."
    execute_command ${execute_user} ${ip} "${sys_bindir}/ksql -d test -U${db_user} -p ${db_port} -f ${install_dir}/kmonitor/scripts/UserInit.sql"
    [ $? -ne 0 ] && exit 0
    echo "[INSTALL] execute UserInit.sql on  \"${ip}\" ... OK"
    echo "[INSTALL]  execute FunctionInit_V8R6.sql on \"${ip}\" ..."
    if [ "${db_mode}"x == "oracle"x ]
    then
        execute_command ${execute_user} ${ip} "${sys_bindir}/ksql -d test -U${db_user} -p ${db_port} -f ${install_dir}/kmonitor/scripts/FunctionInit_V8R6_oracle.sql"
    else
        execute_command ${execute_user} ${ip} "${sys_bindir}/ksql -d test -U${db_user} -p ${db_port} -f ${install_dir}/kmonitor/scripts/FunctionInit_V8R6_pg.sql"
    fi
    [ $? -ne 0 ] && exit 0
    echo "[INSTALL] execute  execute FunctionInit_V8R6.sql on  \"${ip}\" ... OK"
    # 配置kmonitor.properties
    set_or_update_parm  ${ip} ${super_user} ${config_path} "KINGBASE_PORT=${db_port}"
    set_or_update_parm  ${ip} ${super_user} ${config_path} "KMONITOR_SERVER=(\"kingbase_exporter\" \"node_exporter\")"
    # 配置application.yaml  这里有坑  需要考虑对齐的问题 todo
    #set_or_update_parm  ${ip} ${kmonitor_user} ${config_path} "esrepPasswd: ${esrep_password}"
}
function start_kmonitor_local()
{
    local ip=$1
    local config_path="${install_dir}/kmonitor/kmonitor.properties"
    [ "${kmonitor_enable}"x != "true"x ] && return
    # 启动系统服务
    echo "[INSTALL]  chkconfig --add kingbase_exporter on \"${ip}\" ..."
    execute_command ${super_user} ${ip}  "chkconfig --add kingbase_exporter; "
    [ $? -ne 0 ] && exit 1
    echo "[INSTALL]  chkconfig --add kingbase_exporter on \"${ip}\" ...OK"

    echo "[INSTALL]  chkconfig --add node_exporter on \"${ip}\" ..."
    execute_command ${super_user} ${ip}  "chkconfig --add node_exporter; "
    [ $? -ne 0 ] && exit 1
    echo "[INSTALL]  chkconfig --add node_exporter on \"${ip}\" ...OK"

    echo "[INSTALL]  systemctl enable kingbase_exporter on \"${ip}\" ..."
    execute_command ${super_user} ${ip}  "systemctl enable kingbase_exporter; "
    [ $? -ne 0 ] && exit 1
    echo "[INSTALL]  systemctl enable kingbase_exporter on \"${ip}\" ...OK"

    echo "[INSTALL]  systemctl enable node_exporter on \"${ip}\" ..."
    execute_command ${super_user} ${ip}  "systemctl enable node_exporter;"
    [ $? -ne 0 ] && exit 1
    echo "[INSTALL]  systemctl enable node_exporter on \"${ip}\" ...OK"


    execute_command ${super_user} ${ip}  "bash ${install_dir}/kmonitor/kmonitor.sh start"
    echo "[INSTALL] start cron monitor cronjob on \"${ip}\" ... "
    local cron_command="*/1 * * * * root . /etc/profile;source ${env_file};bash ${install_dir}/kmonitor/kmonitor.sh start >/dev/null 2>&1; done"
    start_cron "$ip" "$cron_command"
    [ $? -ne 0 ] && return 1
    echo "[INSTALL] start cron monitor cronjob on \"${ip}\" ... OK"

 }
function register_kmonitor_local()
{
    local ip=$1
    local config_path="${install_dir}/kmonitor/kmonitor.properties"
    local clusterName=`getRelname`"Cluster"
    [ "${kmonitor_enable}"x != "true"x ] && return
    #清理unregister列表
    set_or_update_parm  ${ip} ${super_user} ${config_path} "DEREGISTER_LIST=()"
    #配置register列表
    [ "${ip}"x != "${backup_host}"x ] && set_or_update_parm  ${ip} ${super_user} ${config_path} "REGISTER_KINGBASE_LIST=(\"${ip}_1234\")"
    set_or_update_parm  ${ip} ${super_user} ${config_path} "REGISTER_NODE_LIST=(\"${ip}_9100\")"
    set_or_update_parm  ${ip} ${super_user} ${config_path} "CLUSTER=\"${clusterName}\""
    set_or_update_parm  ${ip} ${super_user} ${config_path} "KMONITOR_SERVER_ADDRESS=\"${kmonitor_server_address}\""

    echo "[INSTALL]  register to kmonitor:${KMONITOR_SERVER_ADDRESS} on \"${ip}\" ..."
    execute_command ${super_user} ${ip}  "bash ${install_dir}/kmonitor/scripts/register.sh"
    [ $? -ne 0 ] && return 0
    echo "[INSTALL]  register to kmonitor:${KMONITOR_SERVER_ADDRESS} on \"${ip}\" ...OK"
}

function unregister_kmonitor_local()
{
    local ip=$1
    local config_path="${install_dir}/kmonitor/kmonitor.properties"
    [ "${kmonitor_enable}"x != "true"x ] && return
    #清理unregister列表
    set_or_update_parm  ${ip} ${super_user} ${config_path} "REGISTER_KINGBASE_LIST=()"
    set_or_update_parm  ${ip} ${super_user} ${config_path} "REGISTER_NODE_LIST=()"
    #配置register列表
    if [ "${ip}"x != "${backup_host}"x ]
    then
        set_or_update_parm  ${ip} ${super_user} ${config_path} "DEREGISTER_LIST=(\"${ip}_1234\" \"${ip}_9100\")"
    else
        set_or_update_parm  ${ip} ${super_user} ${config_path} "DEREGISTER_LIST=(\"${ip}_9100\")"
    fi
    echo "[INSTALL]  unregister to kmonitor:${KMONITOR_SERVER_ADDRESS} on \"${ip}\" ..."
    execute_command ${super_user} ${ip}  "bash ${install_dir}/kmonitor/scripts/register.sh"
    [ $? -ne 0 ] && return 0
    echo "[INSTALL]  unregister to kmonitor:${KMONITOR_SERVER_ADDRESS} on \"${ip}\" ...OK"
}

function standby_clone()
{
    # clone备机
    echo "[INSTALL] clone and start up the standby ..."
    for ip in ${all_ip[@]}
    do
        [ "${ip}"x = "${primary_host}"x ] && continue
        [ "${ip}"x = "${backup_host}"x ] && continue
        echo "clone the standby on \"${ip}\" ..."
        echo "${sys_bindir}/repmgr -h ${primary_host} -U esrep -d esrep -p ${db_port} standby clone"
        execute_command ${execute_user} ${ip} "${sys_bindir}/repmgr -h ${primary_host} -U esrep -d esrep -p ${db_port} standby clone"
        [ $? -ne 0 ] && exit 0
        echo "clone the standby on \"${ip}\" ... OK"
        echo "start up the standby on \"${ip}\" ..."
        echo "${sys_bindir}/sys_ctl -w -t 60 -l ${install_dir}/logfile -D ${data_directory} start"
        execute_command ${execute_user} ${ip} "${sys_bindir}/sys_ctl -w -t 60 -l ${install_dir}/logfile -D ${data_directory} start"
        [ $? -ne 0 ] && exit 0
        echo "start up the standby on \"${ip}\" ... OK"
        echo "register the standby on \"${ip}\" ..."
        execute_command ${execute_user} ${ip} "${sys_bindir}/repmgr standby register -F"
        [ $? -ne 0 ] && exit 0
        echo "[INSTALL] register the standby on \"${ip}\" ... OK"
    done
}

function start_cluster()
{
    # 启动集群
    echo "[INSTALL] start up the whole cluster ..."
    if [ $REPLICA_COUNT -eq 2 ]
    then
        execute_command ${execute_user} ${primary_host} "${sys_bindir}/sys_monitor.sh start"
    else
        execute_command ${execute_user} ${init_host} "${sys_bindir}/sys_monitor.sh start"
    fi
    [ $? -ne 0 ] && exit 0
    echo "[INSTALL] start up the whole cluster ... OK"
    echo "[INSTALL] fork a child process to do backup init ..."
}

function shrink_nocheck()
{
    JudgeTodo
    pre_exe
    [ $? -ne 0 ] && exit 0
    local should_exit=0
    if [ "${primary_host}"x = ""x ]
    then
        primary_host="${all_ip[0]}"
    fi
    if [ "${backup_host}"x = ""x -a $rep_num -gt 1 ]
    then
        backup_host="${all_ip[1]}"
    fi
    if [ "${init_host}"x = ""x ];
    then
        init_host="${all_ip[-1]}"
    fi
    #检查主库是否存在
    deploy_by_sshd=1
    check_master_node_exists
    echo "cur_node_id:"`get_current_node_id ${all_ip[$pod_index]}`
    cur_node_id=`get_current_node_id ${all_ip[$pod_index]}`
    drop_standby_node "$cur_primary_host" "${all_ip[$pod_index]}" "$cur_node_id"
    scale_rman "delnode" "$cur_primary_host" "${all_ip[$pod_index]}"
    unregister_kmonitor_local "${all_ip[$pod_index]}"
}

function update_heart_beat()
{
    JudgeTodo
    pre_exe
    [ $? -ne 0 ] && exit 0

    local should_exit=0
    if [ "${primary_host}"x = ""x ]
    then
        primary_host="${all_ip[0]}"
    fi
    if [ "${backup_host}"x = ""x ]
    then
        backup_host="${all_ip[1]}"
    fi
    if [ "${init_host}"x = ""x ];
    then
        init_host="${all_ip[-1]}"
    fi

    local i=0
    for ip in ${all_ip[@]}
    do
        [ "${ip}"x = "${backup_host}"x ] && let i++ &&continue
        if [ ${pod_index} -eq $i ]
        then
            local ip_addr=$ip
            local node_id=$(read_conf_value ${ip} ${repmgr_conf} node_id)
            local heart_beat_file='heart_beat_node'${node_id}
            local date=$(date "+%Y-%m-%d %H:%M:%S")
            break
        fi
        let i++
    done

    #检查数据库是否启动
    local db_running=`netstat -apn 2>/dev/null|grep -w "${db_port}"|wc -l`
    if [ $? -ne 0 -o "${db_running}"x == "0"x ]
    then
        echo "[WARNING] the db on \"${ip}:${db_port}\" is not running"
        execute_command ${super_user} $primary_host "rm -rf ${install_dir}/etc/$heart_beat_file"
        return 0
    fi
    echo "date='$date' node_id='$node_id  ip_addr=$ip_addr heart_beat_file:$heart_beat_file"
    execute_command ${super_user} $primary_host "test -f ${install_dir}/etc/$heart_beat_file"
    [ $? -ne 0 ] && config_auto_heartbeat_local "$ip_addr"
    echo "[INSTALL] update the $heart_beat_file on \"${primary_host}\" ..."
    set_or_update_parm "$primary_host" ${execute_user} "${install_dir}/etc/$heart_beat_file" "date='$date'"
    [ $? -ne 0 ] && config_auto_heartbeat_local "$ip_addr"
    set_or_update_parm "$primary_host" ${execute_user} "${install_dir}/etc/$heart_beat_file" "node_id='$node_id'"
    [ $? -ne 0 ] && config_auto_heartbeat_local "$ip_addr"
    set_or_update_parm "$primary_host" ${execute_user} "${install_dir}/etc/$heart_beat_file" "survival_flag=true"
    [ $? -ne 0 ] && config_auto_heartbeat_local "$ip_addr"
    set_or_update_parm "$primary_host" ${execute_user} "${install_dir}/etc/$heart_beat_file" "ip_addr=$ip_addr"
    [ $? -ne 0 ] && config_auto_heartbeat_local "$ip_addr"
    execute_command ${super_user} $primary_host "rm -rf ${install_dir}/etc/sed*"
    echo "[INSTALL] update the $heart_beat_file on \"${primary_host}\" ... OK"
}

function start_cron()
{
    local host=$1
    local i=0
    local cron_command=$2
    #  root用户添加CRON任务
    local cronexist=`execute_command ${super_user} $host "cat $cron_file 2>/dev/null| grep -wFn \"${cron_command}\" |wc -l"`
    if [ "$cronexist"x != ""x ] && [ $cronexist -eq 1 ]
    then
        local realist=`execute_command ${super_user} $host "cat $cron_file | grep -wFn \"${cron_command}\""`
        local linenum=`execute_command ${super_user} $host "echo \"${realist}\" |awk -F':' '{print \\$1}'"`
        execute_command ${super_user} $host "sed \"${linenum}s/#*//\" $cron_file > ${install_dir}/etc/crontab.bak"
        execute_command ${super_user} $host "cat ${install_dir}/etc/crontab.bak > $cron_file"
        execute_command ${super_user} $host "rm -rf ${install_dir}/etc/crontab.bak"
    elif [ "$cronexist"x != ""x ] && [ $cronexist -eq 0 ]
    then
        execute_command ${super_user} $host "echo \"${cron_command}\" >> $cron_file"
    else
        return 1
    fi
    if [ "${cron_name}x" != ""x ]
    then
        execute_command ${super_user} $host "service ${cron_name} restart 2>/dev/null"
    fi
    return 0
}

function stop_cron()
{
    local host=$1
    local cron_command=$2
    ## TODO
    #  root用户注释CRON任务
    local cronexist=`execute_command ${super_user} $host "cat $cron_file 2>/dev/null| grep -wFn \"${cron_command}\" |wc -l"`
    if [ "$cronexist"x != ""x ] && [ $cronexist -eq 1 ]
    then
        local realist=`execute_command ${super_user} $host "cat $cron_file | grep -wFn \"${cron_command}\""`
        local linenum=`execute_command ${super_user} $host "echo \"${realist}\" |awk -F':' '{print \\$1}'"`

        execute_command ${super_user} $host "sed \"${linenum}s/^/#/\" $cron_file > ${install_dir}/etc/crontab.bak"
        execute_command ${super_user} $host "cat ${install_dir}/etc/crontab.bak > $cron_file"
        execute_command ${super_user} $host "rm -rf ${install_dir}/etc/crontab.bak"
    fi
    return 0
}

function config_auto_heartbeat()
{
    config_envfile
    for ip in ${all_ip[@]}
    do
        [ "${ip}"x == "$backup_host"x ] && continue
        local node_id=$(read_conf_value ${ip} ${repmgr_conf} node_id)
        local heart_beat_file='heart_beat_node'${node_id}
        local date=$(date "+%Y-%m-%d %H:%M:%S")
        echo "[INSTALL] write the $heart_beat_file on \"${primary_host}\" ..."
        execute_command ${execute_user} $primary_host "cat >> ${install_dir}/etc/$heart_beat_file <<_REPEOF
node_id=$node_id
date=\"$date\"
survival_flag=true
ip_addr=${ip}
_REPEOF"
        echo "[INSTALL] write the $heart_beat_file on \"${primary_host}\" ... OK"
        echo "[INSTALL] start cron update the $heart_beat_file on \"${primary_host}\" ..."
        local cron_command="*/1 * * * * kingbase . /etc/profile;source ${env_file};for ((i = 1; i <= 4; i++));do sleep 12;${install_dir}/bin/docker-entrypoint.sh update_heart_beat >/dev/null 2>&1; done"
        start_cron "$ip" "$cron_command"
        [ $? -ne 0 ] && exit 1
        echo "[INSTALL] start cron update the $heart_beat_file on \"${primary_host}\" ... OK"
    done
}

function config_auto_heartbeat_local()
{
    local ip=$1
    [ "${repmgr_conf}"x = ""x ] && repmgr_conf=${install_dir}/etc/repmgr.conf
    echo  "read_conf_value_local ${repmgr_conf} node_id"
    local node_id=$(read_conf_value_local ${repmgr_conf} node_id)
    local ip_addr=`cat ${repmgr_conf}|grep -awE conninfo|tail -n 1|awk -F "'" '{print $2}' |awk -F "=" '{ print $2 }'|awk -F " " ' { print $1 } ' `
    local heart_beat_file='heart_beat_node'${node_id}
    local date=$(date "+%Y-%m-%d %H:%M:%S")
    execute_command ${execute_user} $primary_host "rm ${install_dir}/etc/$heart_beat_file -rf"
    execute_command ${execute_user} $primary_host "cat >> ${install_dir}/etc/$heart_beat_file <<_REPEOF
node_id=$node_id
date=\"$date\"
survival_flag=true
ip_addr=${ip_addr}
_REPEOF"
    echo "[INSTALL] write the $heart_beat_file on \"${primary_host}\" ... OK"
    execute_command ${execute_user} $primary_host "cat ${install_dir}/etc/$heart_beat_file"
    echo "[INSTALL] start cron update the $heart_beat_file on \"${ip_addr}\" ..."
    local cron_command="*/1 * * * * kingbase . /etc/profile;source ${env_file};for ((i = 1; i <= 4; i++));do sleep 12;${install_dir}/bin/docker-entrypoint.sh update_heart_beat >/dev/null 2>&1; done"
    start_cron "$ip_addr" "$cron_command"
    if [ $? -ne 0 ]
    then
       echo "[INSTALL] start cron update the $heart_beat_file on \"${ip_addr}\" ... fail"
       sleep 5
       exit 1
    fi
    echo "[INSTALL] start cron update the $heart_beat_file on \"${ip_addr}\" ... OK"
}

function config_auto_backup()
{
    [ "${backup_host}"x == ""x ] && return
    (
    # 配置自动备份
    echo "[BACKUP] child: wait the primary host can ping backup host ..."
    should_exit=0
    for((i=1;i<=10;i++))
    do
        execute_command ${execute_user} ${primary_host} "ping -c 3 ${backup_host} >/dev/null"
        if [ $? -ne 0 ]
        then
            should_exit=1
            echo "[BACKUP] can not ping \"${backup_host}\" on primary host \"${primary_host}\""
            echo "[BACKUP] failed to ping ($i / 10)"
            [ $i -eq 10 ] && break
            echo "[BACKUP] sleep 6 seconds and ping again ..."
            sleep 6
        else
            should_exit=0
            echo "[BACKUP] success to ping the backup host \"${backup_host}\" on primary host \"${primary_host}\" ..... OK"
            break
        fi
    done
    [ $should_exit -eq 1 ] && exit 0

    echo "[BACKUP] child: set the backup config file ..."
    execute_command ${execute_user} ${backup_host} "cat >> ${sys_bindir}/../share/sys_backup.conf <<_BAKEOF
#file: sys_backup.conf
#dest dir: <cluster_dir>/kingbase/bin/sys_backup.conf
#dest dir: <cluster_dir>/kingbase/share/sys_backup.conf

# target db style enum:  single/cluster
_target_db_style=\"cluster\"

# one kingbase node IP
# just provide one IP, script will use 'repmgr cluster show' get other node IP
_one_db_ip=\"${primary_host}\"

# local repo IP, can be same as one_db_ip, means repo located in one db node
_repo_ip=\"${backup_host}\"
# label of this cluster
_stanza_name=\"kingbase\"
# OS user name of database
_os_user_name=\"kingbase\"

# !!!! dir to store the backup files
# should be accessable for the OS user
_repo_path=\"${backup_dir}\"

# count of keep, over the count FULL-backup will be remove
_repo_retention_full_count=5
# count of days, interval to do FULL-backup
_crond_full_days=7
# count of days, interval to do DIFF-backup
_crond_diff_days=0
# count of days, interval to do INCR-backup
_crond_incr_days=1
# HOUR to do the FULL-backup
_crond_full_hour=2
# HOUR to do the DIFF-backup
_crond_diff_hour=3
# HOUR to do the INCR-backup
_crond_incr_hour=4
_use_scmd='off'

# OS cmd define
_os_ip_cmd=\"/sbin/ip\"
_os_rm_cmd=\"/bin/rm\"
_os_sed_cmd=\"/usr/bin/sed\"
_os_grep_cmd=\"/bin/grep\"
_BAKEOF"
    [ $? -ne 0 ] && exit 0
    echo "[BACKUP] child: set the backup config file ... OK"

    # 初始化自动备份
    echo "[BACKUP] child: init for the backup ..."
    execute_command ${execute_user} ${backup_host} "${sys_bindir}/sys_backup.sh init"
    [ $? -ne 0 ] && exit 0
    echo "[BACKUP] child: init for the backup ... OK"

    # 启动自动备份定时任务
    echo "[BACKUP] child: start the crontab of the backup ..."
    execute_command ${execute_user} ${backup_host} "${sys_bindir}/sys_backup.sh start"
    [ $? -ne 0 ] && exit 0
    echo "[BACKUP] child: start the crontab of the backup ... OK"
    echo "[BACKUP] child: ALL DONE"
    ) &
}

function initializing()
{
    copy_soft_ware

    check_install_dir

    check_and_change_arping

    mkdir_archive

    init_db

    config_kingbase_conf

    config_sys_hba_conf

    config_repmgr_conf

    config_nodetools_conf

    config_encpwd

    config_auto_heartbeat

    start_db "$primary_host"

    standby_clone

    start_cluster

    init_kmonitor_local ${primary_host}

    start_cluster_kmonitor

    config_auto_backup
}

function main()
{
    JudgeTodo
    pre_exe
    [ $? -ne 0 ] && exit 0

    local should_exit=0

    if [ "${primary_host}"x = ""x ]
    then
        primary_host="${all_ip[0]}"
    fi
    if [ "${backup_host}"x = ""x -a $REPLICA_COUNT -gt 1 ]
    then
        backup_host="${all_ip[1]}"
    fi
    if [ "${init_host}"x = ""x ];
    then
        init_host="${all_ip[-1]}"
    fi
    # 等待sshd启动
    # sleep 20

    check_net

    choose_exec_mode

    echo "[INSTALL] the main process exit right now .."
    echo "[INSTALL] DONE"
}
case $1 in
"shrink")
    function_name="shrink"
    main
    exit 0
    ;;
"shrink_nocheck")
    function_name="update_heart_beat"
    shrink_nocheck
    exit 0
    ;;
"update_heart_beat")
    function_name="update_heart_beat"
    update_heart_beat
    exit 0
    ;;
"")
    usersetip=$*
    main
    exit 0
    ;;
*)
    echo "Do not choose any method, shrink/shrink-nocheck/update_heart_beat or null !"
    exit 1
    ;;
esac
exit 0